using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MyDotNetFrameworkWpfCrasher;

/// <summary>The crash shapes the sample can trigger.</summary>
internal static class Crashers
{
    // A non-canonical x64 address (bits 63:48 are not a sign extension of bit 47) is
    // unmappable in every process, so a write to it faults deterministically. A null or
    // near-null pointer would not do: the CLR turns null-page faults into a catchable
    // NullReferenceException.
    public const ulong UnmappedAddress = 0xDEADBEEFDEADBEEF;

    /// <summary>An ordinary unhandled managed exception. BugSplat's native in-process filter
    /// captures a minidump.</summary>
    public static void UnhandledManagedException() =>
        throw new InvalidOperationException("MyDotNetFrameworkWpfCrasher managed crash");

    /// <summary>Throws for the caller to catch and report with BugSplat.Post. Kept out of line
    /// so the report shows this frame above the handler.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void HandledException() =>
        throw new InvalidOperationException("MyDotNetFrameworkWpfCrasher non-crash error");

    /// <summary>Stops the calling thread from pumping messages until the process is ended.
    /// Invoked on the UI thread, BugSplat's hang detection reports it and then terminates the
    /// process. The monitor polls every 5 seconds and needs 5 more of unresponsiveness, so a
    /// freeze of a fixed few seconds can end between polls and never be reported.</summary>
    public static void Hang()
    {
        while (true) Thread.Sleep(10);
    }
}
