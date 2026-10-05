using System.Runtime.InteropServices;

namespace MyDotNetFrameworkWpfCrasher;

// P/Invoke surface for MyDotNetCrasherNative.dll (flat C ABI, __cdecl), used by the
// Mixed-Mode and Heap Corruption cards to fault across the C#/C++ boundary. The DLL is copied next to the
// executable by the project build.
internal static class NativeMethods
{
    private const string Dll = "MyDotNetCrasherNative.dll";

    // Managed -> P/Invoke -> native access violation: the crash stack crosses the boundary
    // (native frame on top, managed frames below the transition).
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_access_violation();

    // A C++ library frees the same heap block twice. Windows detects the corrupted heap and
    // fail-fasts the process straight through WER, bypassing every in-process handler.
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void bscrash_heap_double_free();
}
