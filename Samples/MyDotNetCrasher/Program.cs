using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using BugSplatDotNet;

// MyDotNetCrasher — demonstrates BugSplatDotNet crash reporting. Every report is a minidump:
//   native   : a hardware access violation the CLR never surfaces  -> native SDK captures a minidump
//   managed  : an unhandled C# exception                           -> native SDK captures a minidump
//   handled  : a caught C# exception reported with BugSplat.Post   -> minidump, process keeps running
//   feedback : non-crashing user feedback                          -> returns the report id
//
// Extra crash shapes (bugsplat-cdb .NET 10 cDAC fixtures) fault deep enough that the
// captured minidump exercises the managed stack walk / naming:
//   deep     : native AV several managed frames deep (full IP->method walk fixture)
//   generic  : throw from a closed generic method on a generic type (closed-instantiation spelling)
//   lambda   : throw from a LINQ lambda (compiler-generated <>c naming)
//   async    : throw from an async state machine (MoveNext naming)
//
// Mixed-mode shapes (P/Invoke into MyDotNetCrasherNative.dll) whose crash stack crosses the
// C#/C++ boundary — the cases BugSplat and bugsplat-cdb symbolicate as one unified stack:
//   native-av        : managed -> native access violation (native frame on top)
//   native-deep      : several native C++ frames beneath the managed transition
//   native-callback  : managed -> native -> managed callback that throws (M/N/M interleave)
//   cpp-throw        : uncaught C++ exception in native code
//   native-so        : cross-boundary stack overflow (native <-> managed mutual recursion)
//   native-thread    : a native background thread with no managed frames faults
//
// WER-class mixed-mode shapes — fail-fast crashes the OS routes straight through WerFault,
// bypassing the CLR and BugSplat's in-process filter, so they are captured only by the
// allowlisted BugSplatWer.dll runtime-exception helper (mirror MyConsoleCrasher's WER trio):
//   native-fastfail  : __fastfail from native code
//   native-overrun   : /GS stack-cookie smash (native StackOverrun)
//   native-double-delete : heap-metadata corruption via double free (native DoubleDelete)
//
// The mixed-mode modes require MyDotNetCrasherNative.dll: build ..\MyDotNetCrasherNative
// (x64, MSBuild) first, then this project copies it next to the exe.
//
//   dotnet run -- native   (managed | handled | feedback | deep | generic | lambda | async |
//                           native-av | native-deep | native-callback | cpp-throw |
//                           native-so | native-thread | native-fastfail |
//                           native-overrun | native-double-delete)

var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");

var bugsplat = new BugSplatDotNet.BugSplat("Fred", "MyDotNetCrasher", "1.0.0")
{
    User = "sample-user",
    Email = "sample-user@example.com",
    Description = $"MyDotNetCrasher {mode} crash",
};
bugsplat.SetAttribute("runtime", RuntimeInformation.FrameworkDescription);

// Unattended runs: never block on the native crash dialog.
bugsplat.QuietMode = true;

switch (mode)
{
    case "feedback":
        var result = bugsplat.PostFeedback("MyDotNetCrasher feedback", "Hello from .NET 10 via BugSplatDotNet.");
        Console.WriteLine($"Feedback posted: success={result.Success}, crashId={result.CrashId}");
        Console.WriteLine($"  {result.InfoUrl}");
        break;

    case "handled":
        // A caught exception reported from inside its catch, while the throw-site frames are
        // still on the stack: a minidump, and the process keeps running.
        try
        {
            Crashers.Run(mode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught '{ex.Message}', reporting it with BugSplat.Post...");
            Console.WriteLine(bugsplat.Post(ex) ? "Posted; still running." : "Post failed.");
        }
        break;

    default:
        if (!Crashers.IsCrashMode(mode))
        {
            Console.WriteLine("usage: MyDotNetCrasher [native | managed | handled | feedback | deep | generic | lambda | async |");
            Console.WriteLine("                       native-av | native-deep | native-callback | cpp-throw |");
            Console.WriteLine("                       native-so | native-thread | native-fastfail |");
            Console.WriteLine("                       native-overrun | native-double-delete]");
            break;
        }
        Crashers.Run(mode);
        break;
}

// Every crash runs a few frames down, through SampleStackFrame0-2, and then faults in a method
// named after the crash (ThrowUnhandledManagedException, CrashNativeAccessViolation, ...), so a
// report shows a real call stack whose top frame says what happened. NoInlining throughout:
// these are one-line calls the JIT would otherwise fold away in a Release build.
internal static class Crashers
{
    // A non-canonical x64 address (bits 63:48 are not a sign extension of bit 47) is
    // unmappable in every process, so a write to it faults deterministically regardless
    // of the process's address-space layout.
    public const ulong UnmappedAddress = 0xDEADBEEFDEADBEEF;

    private static readonly HashSet<string> Modes =
    [
        "native", "managed", "handled", "deep", "generic", "lambda", "async",
        "native-av", "native-deep", "native-callback", "cpp-throw", "native-so",
        "native-thread", "native-fastfail", "native-overrun", "native-double-delete",
    ];

    public static bool IsCrashMode(string mode) => Modes.Contains(mode);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Run(string mode) => SampleStackFrame0(mode);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SampleStackFrame0(string mode) => SampleStackFrame1(mode);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SampleStackFrame1(string mode) => SampleStackFrame2(mode);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void SampleStackFrame2(string mode)
    {
        // What this crash is supposed to look like, written before it happens, because nothing
        // can reconstruct the managed half afterwards. The header also records whether this
        // build is JIT, ReadyToRun or NativeAOT, which is what decides the symbolication path
        // the fixture exercises — see BugSplat-Git/bugsplat-cdb#14. Recorded here, one frame
        // above the crash method, so it includes the sample frames; the shapes that fault
        // further down (deep, generic, lambda, async) add their own frames below it.
        CrashExpectation.Record(mode);

        switch (mode)
        {
            case "native": Console.WriteLine("Triggering a native access violation..."); CrashAccessViolation(); break;
            case "managed": Console.WriteLine("Throwing an unhandled managed exception..."); ThrowUnhandledManagedException(); break;
            case "handled": ThrowHandledException(); break;
            case "deep": Console.WriteLine("Faulting several managed frames deep..."); DeepA(5); break;
            case "generic": Console.WriteLine("Throwing from a closed generic method..."); new Box<int>().Fail<string>(7); break;
            case "lambda": Console.WriteLine("Throwing from a LINQ lambda..."); ThrowFromLambda(); break;
            case "async": Console.WriteLine("Throwing from an async state machine..."); ThrowFromAsync().GetAwaiter().GetResult(); break;
            case "native-av": Console.WriteLine("Managed -> native access violation..."); CrashNativeAccessViolation(); break;
            case "native-deep": Console.WriteLine("Managed -> several native frames -> access violation..."); CrashNativeDeep(); break;
            case "native-callback": Console.WriteLine("Managed -> native -> managed callback that throws (M/N/M)..."); CrashNativeCallback(); break;
            case "cpp-throw": Console.WriteLine("Uncaught C++ exception in native code..."); CrashCppThrow(); break;
            case "native-so": Console.WriteLine("Cross-boundary stack overflow (native <-> managed recursion)..."); CrashNativeStackOverflow(); break;
            case "native-thread": Console.WriteLine("Native background thread (no managed frames) faults..."); CrashNativeThread(); break;
            case "native-fastfail": Console.WriteLine("__fastfail from native code (WER)..."); CrashNativeFastFail(); break;
            case "native-overrun": Console.WriteLine("Native /GS stack-cookie smash (WER)..."); CrashNativeStackOverrun(); break;
            case "native-double-delete": Console.WriteLine("Native heap corruption via double free (WER)..."); CrashNativeDoubleDelete(); break;
        }
    }

    // A write to an address that can never be mapped raises a non-catchable
    // AccessViolationException; the runtime fail-fasts and BugSplat's native handler captures
    // the minidump. (A null pointer would not do: the CLR turns null-page faults into a
    // catchable NullReferenceException that the native handler never sees.)
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void CrashAccessViolation() => *(int*)UnmappedAddress = 42;

    // An ordinary unhandled C# exception; BugSplat's native handler captures a minidump.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUnhandledManagedException() =>
        throw new InvalidOperationException("MyDotNetCrasher managed crash");

    // Thrown for the handled mode's catch in Main.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowHandledException() =>
        throw new InvalidOperationException("MyDotNetCrasher handled exception");

    // Native AV several managed frames deep: the fault context sits on the crashing managed
    // instruction, so the live stack walk must resolve every app frame.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepA(int n) => DeepB(n - 1);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepB(int n) => DeepC(n - 1);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepC(int n) => DeepD(n - 1);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DeepD(int n) => DeepFault(n - 1);
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static unsafe void DeepFault(int n) => *(int*)UnmappedAddress = n;

    // Closed generic method on a closed generic type — exercises instantiation spelling.
    public sealed class Box<TValue>
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Fail<TMethod>(int depth) =>
            throw new InvalidOperationException($"generic crash box={typeof(TValue)} m={typeof(TMethod)} d={depth}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowFromLambda()
    {
        var data = new[] { 1, 2, 3 };
        foreach (var _ in data.Select(x => x == 2 ? throw new DivideByZeroException("lambda crash") : x))
        {
        }
    }

    private static async Task ThrowFromAsync()
    {
        await Task.Yield();
        throw new NotSupportedException("async crash");
    }

    // Mixed-mode (C#/C++) crashes: each P/Invokes into MyDotNetCrasherNative.dll from a managed
    // method named after the crash, so the managed frame under the native ones says which it is.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeAccessViolation() => NativeMethods.bscrash_access_violation();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeDeep() => NativeMethods.bscrash_deep();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeCallback() => NativeMethods.bscrash_invoke_callback(MixedMode.ThrowCallback);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashCppThrow() => NativeMethods.bscrash_cpp_throw();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeStackOverflow() => NativeMethods.bscrash_recurse_via_callback(MixedMode.RecurseCallback);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeThread() => NativeMethods.bscrash_background_thread();

    // WER-class crashes: fail-fasts the OS routes straight through WerFault, bypassing the CLR
    // and BugSplat's in-process filter, so only BugSplatWer.dll captures them.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeFastFail() => NativeMethods.bscrash_fastfail();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeStackOverrun() => NativeMethods.bscrash_stack_overrun();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CrashNativeDoubleDelete() => NativeMethods.bscrash_double_delete();
}

// Managed side of the mixed-mode (C#/C++) crashes: the callbacks the native DLL invokes so a
// crash stack can go managed -> native -> managed.
internal static class MixedMode
{
    // Thrown from inside a native-invoked callback: the unhandled exception's stack interleaves
    // managed (this method) / native (bscrash_invoke_callback) / managed (Main).
    public static void ThrowCallback() =>
        throw new InvalidOperationException("managed callback crash (mixed-mode M/N/M)");

    // Re-enters the native recurser, so native and managed frames alternate until the stack
    // overflows.
    public static void RecurseCallback() =>
        NativeMethods.bscrash_recurse_via_callback(RecurseCallback);
}

// P/Invoke surface for MyDotNetCrasherNative.dll (flat C ABI, __cdecl). The DLL is copied
// next to this executable by the project's build (see MyDotNetCrasher.csproj).
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void NativeCallback();

internal static class NativeMethods
{
    private const string Dll = "MyDotNetCrasherNative.dll";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_access_violation();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_deep();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_invoke_callback(NativeCallback cb);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_cpp_throw();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_recurse_via_callback(NativeCallback cb);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_background_thread();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_fastfail();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_stack_overrun();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_double_delete();
}
