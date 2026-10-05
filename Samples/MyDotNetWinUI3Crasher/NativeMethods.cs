using System.Runtime.InteropServices;

namespace MyDotNetWinUI3Crasher;

// P/Invoke surface for MyDotNetCrasherNative.dll (flat C ABI, __cdecl), used by the
// Mixed-Mode card to fault across the C#/C++ boundary. The DLL is copied next to the
// executable by the project build; see the console MyDotNetCrasher for the full mode set.
internal static class NativeMethods
{
    private const string Dll = "MyDotNetCrasherNative.dll";

    // Managed -> P/Invoke -> native access violation: the crash stack crosses the boundary
    // (native frame on top, managed frames below the transition).
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_access_violation();
}
