using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyDotNetWinUI3Crasher;

/// <summary>
/// The crash shapes the sample can trigger. Mirrors the console MyDotNetCrasher so both
/// samples exercise the same paths through BugSplatDotNet.
/// </summary>
internal static class Crashers
{
    // A non-canonical x64 address (bits 63:48 are not a sign extension of bit 47) is
    // unmappable in every process, so a write to it faults deterministically. A null or
    // near-null pointer would not do: the CLR turns null-page faults into a catchable
    // NullReferenceException that the native handler never sees.
    public const ulong UnmappedAddress = 0xDEADBEEFDEADBEEF;

    /// <summary>A hardware access violation the CLR never surfaces as a managed exception,
    /// so BugSplat's native handler captures a minidump.</summary>
    public static unsafe void NativeAccessViolation() => *(int*)UnmappedAddress = 42;

    /// <summary>An ordinary unhandled managed exception. WinUI fail-fasts, and BugSplat
    /// captures a minidump through WER.</summary>
    public static void UnhandledManagedException() =>
        throw new InvalidOperationException("MyDotNetWinUI3Crasher managed crash");

    /// <summary>Throws for the caller to catch and report with BugSplat.Post. Kept out of line
    /// so the report shows this frame above the handler.</summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public static void HandledException() =>
        throw new InvalidOperationException("MyDotNetWinUI3Crasher non-crash error");

    /// <summary>Stops the calling thread from pumping messages until the process is ended.
    /// Invoked on the UI thread, BugSplat's hang detection reports it and then terminates the
    /// process. The monitor polls every 5 seconds and needs 5 more of unresponsiveness, so a
    /// freeze of a fixed few seconds can end between polls and never be reported.</summary>
    public static void Hang()
    {
        while (true) Thread.Sleep(10);
    }

    // --- Extra shapes for parity with the console sample's cDAC fixtures ---

    public static void DeepA(int n) => DeepB(n - 1);
    private static void DeepB(int n) => DeepC(n - 1);
    private static void DeepC(int n) => DeepD(n - 1);
    private static void DeepD(int n) => DeepFault(n - 1);
    private static unsafe void DeepFault(int n) => *(int*)UnmappedAddress = n;

    public sealed class Box<TValue>
    {
        public void Fail<TMethod>(int depth) =>
            throw new InvalidOperationException($"generic crash box={typeof(TValue)} m={typeof(TMethod)} d={depth}");
    }

    public static void LambdaCrash()
    {
        var data = new[] { 1, 2, 3 };
        foreach (var _ in data.Select(x => x == 2 ? throw new DivideByZeroException("lambda crash") : x)) { }
    }

    public static async Task AsyncCrash()
    {
        await Task.Yield();
        throw new NotSupportedException("async crash");
    }
}
