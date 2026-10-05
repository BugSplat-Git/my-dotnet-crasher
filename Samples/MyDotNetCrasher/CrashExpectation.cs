using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Records what a crash was supposed to look like, from inside the process, just before
/// it happens.
///
/// A crash dump cannot tell us afterwards. On CoreCLR the managed frames need the DAC to
/// name, on NativeAOT there is no runtime to ask at all and the symbols are ILC-mangled,
/// and under Mono the dump carries no managed metadata whatsoever. The program, on the
/// other hand, knows exactly which methods it called. That makes this file better ground
/// truth than any debugger's output, and it settles inlining and tiering too, because the
/// runtime reports the frames that actually exist rather than the ones that were written.
///
/// The header matters as much as the frames. Which of JIT, ReadyToRun and NativeAOT
/// produced the code decides which symbolication path in bugsplat-cdb has to handle the
/// crash, and a fixture is not interpretable without knowing which one it exercises.
/// </summary>
internal static class CrashExpectation
{
    public const string FileName = "bugsplat-expected-stack.txt";

    /// <summary>
    /// Writes the managed stack as it stands at the call site, plus how this build was
    /// compiled. Call it as close to the crash as possible: from the dispatch point it
    /// records the mode and the compilation facts correctly but sits several frames above
    /// a crash that happens deeper, so the shapes that fault down a call chain should call
    /// it again at the bottom.
    /// </summary>
    public static void Record(string scenario, string? directory = null)
    {
        try
        {
            var text = new StringBuilder();
            text.AppendLine($"# scenario: {scenario}");
            text.AppendLine($"# framework: {RuntimeInformation.FrameworkDescription}");
            text.AppendLine($"# arch: {RuntimeInformation.ProcessArchitecture}");

            // The NativeAOT discriminator, straight from the runtime rather than guessed
            // from the build: an AOT binary has no JIT, so dynamic code is neither
            // supported nor compiled. R2R cannot be told apart at runtime, so the harness
            // passes it in.
            text.AppendLine($"# dynamic-code-supported: {RuntimeFeature.IsDynamicCodeSupported}");
            text.AppendLine($"# dynamic-code-compiled: {RuntimeFeature.IsDynamicCodeCompiled}");
            text.AppendLine($"# build-mode: {Environment.GetEnvironmentVariable("BUGSPLAT_BUILD_MODE") ?? "unset"}");
            text.AppendLine($"# entry-assembly: {Assembly.GetEntryAssembly()?.GetName().Name}");
            text.AppendLine($"# debug-symbols-beside-binary: {HasDebugSymbols()}");
            text.AppendLine($"# utc: {DateTime.UtcNow:O}");
            text.AppendLine($"# thread: {Environment.CurrentManagedThreadId}");
            text.AppendLine();

            // fNeedFileInfo: file and line come from the PDB, so they are present in a
            // build that shipped one and absent otherwise. Frame names do not depend on it.
            var stack = new StackTrace(1, true);
            for (int i = 0; i < stack.FrameCount; i++)
            {
                var frame = stack.GetFrame(i);
                var method = frame?.GetMethod();
                if (method is null)
                {
                    continue;
                }

                var type = method.DeclaringType?.FullName ?? "<global>";
                var file = frame!.GetFileName();
                var where = string.IsNullOrEmpty(file) ? string.Empty : $"  [{file}:{frame.GetFileLineNumber()}]";

                // An identity that survives mangling. When bugsplat-cdb resolves an AOT
                // frame to an ILC name, the token still says which method it really is,
                // so an oracle can assert the right frame without agreeing on spelling.
                var token = TokenOf(method);
                text.AppendLine($"{i,4}  {type}.{method.Name}{where}{token}");
            }

            var path = System.IO.Path.Combine(
                directory ?? AppContext.BaseDirectory, FileName);
            System.IO.File.WriteAllText(path, text.ToString());
            Console.WriteLine($"[BugSplat] expected stack written to {path}");
        }
        catch (Exception ex)
        {
            // Never let recording an expectation change what the scenario does: the crash
            // under test is the point, this file is only evidence about it.
            Console.WriteLine($"[BugSplat] could not write the expected stack: {ex.Message}");
        }
    }

    static string TokenOf(MethodBase method)
    {
        try
        {
            return $"  (module {method.Module.Name} token 0x{method.MetadataToken:x8})";
        }
        catch (Exception)
        {
            // NativeAOT trims metadata, so a token is not always there to read. Its
            // absence is itself worth seeing in the fixture.
            return "  (no metadata token)";
        }
    }

    static bool HasDebugSymbols()
    {
        try
        {
            var assembly = Assembly.GetEntryAssembly();
            var location = assembly?.Location;
            if (string.IsNullOrEmpty(location))
            {
                // Single-file and NativeAOT publish report no location.
                return false;
            }
            return System.IO.File.Exists(System.IO.Path.ChangeExtension(location, ".pdb"));
        }
        catch (Exception)
        {
            return false;
        }
    }
}
