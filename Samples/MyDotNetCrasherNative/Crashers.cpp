// MyDotNetCrasherNative — native C++ crash surface for the .NET samples.
//
// Each export is P/Invoked from C# so the resulting crash dump has a call stack that
// crosses the C#/C++ boundary. These are the cases BugSplat and bugsplat-cdb symbolicate
// as one unified managed+native stack, and the fixtures used to compare bugsplat-cdb against
// the Visual Studio debugger. x64 only, matching the .NET samples.
//
// Flat C ABI (extern "C", __cdecl) so C# can P/Invoke with CallingConvention.Cdecl.

#include <windows.h>
#include <stdexcept>

#define BSNATIVE_API extern "C" __declspec(dllexport)

// Callback the managed side hands us so a stack can go managed -> native -> managed.
typedef void(__cdecl* ManagedCallback)();

// A write to a non-canonical x64 address (bits 63:48 are not a sign extension of bit 47)
// faults in any process; unlike a null-page fault the CLR never converts it to a catchable
// managed exception, so it reaches the native/WER layer.
static volatile int* const kUnmappable = reinterpret_cast<volatile int*>(0xDEADBEEFDEADBEEFull);

// 1. Managed -> P/Invoke -> native access violation. One native frame above the transition.
BSNATIVE_API void __cdecl bscrash_access_violation()
{
    *kUnmappable = 42;
}

// 2. Several distinct native frames beneath the managed transition, then fault.
//    noinline so the chain survives the Release optimizer.
static __declspec(noinline) void DeepD() { *kUnmappable = 4; }
static __declspec(noinline) void DeepC() { DeepD(); }
static __declspec(noinline) void DeepB() { DeepC(); }
static __declspec(noinline) void DeepA() { DeepB(); }
BSNATIVE_API void __cdecl bscrash_deep()
{
    DeepA();
}

// 3. Managed -> native -> managed callback. The callback (a C# method) faults, so the
//    crashing stack interleaves managed/native/managed — the hardest merge case.
BSNATIVE_API void __cdecl bscrash_invoke_callback(ManagedCallback cb)
{
    if (cb) cb();
}

// 4. An uncaught C++ exception. std::terminate fires; the native throw machinery is on the
//    stack above the managed transition.
BSNATIVE_API void __cdecl bscrash_cpp_throw()
{
    throw std::runtime_error("MyDotNetCrasherNative C++ exception");
}

// 5. Cross-boundary stack overflow: native calls the managed callback, which is expected to
//    call back into this function, so the stack grows through alternating managed/native
//    frames until it overflows.
BSNATIVE_API void __cdecl bscrash_recurse_via_callback(ManagedCallback cb)
{
    if (cb) cb();
}

// 6. A native background thread with no managed frames faults, to test thread attribution in
//    a managed process. Returns immediately; the spawned thread crashes the process.
static DWORD WINAPI NativeThreadProc(LPVOID)
{
    *kUnmappable = 6;
    return 0;
}
BSNATIVE_API void __cdecl bscrash_background_thread()
{
    HANDLE h = CreateThread(nullptr, 0, NativeThreadProc, nullptr, 0, nullptr);
    if (h) CloseHandle(h);
    // Give the thread time to run so the process crashes on it rather than returning.
    Sleep(5000);
}

// 7. __fastfail: an immediate fail-fast that routes through WER, from native code.
BSNATIVE_API void __cdecl bscrash_fastfail()
{
    __fastfail(FAST_FAIL_FATAL_APP_EXIT);
}

// --- WER-class crashes ---------------------------------------------------------------
// The next two, like #7, are fail-fast-class: the OS terminates the process straight
// through WerFault (bypassing every SEH/VEH handler, including the CLR's), so BugSplat
// captures them via the allowlisted BugSplatWer.dll runtime-exception helper rather than
// its in-process filter. They mirror MyConsoleCrasher's StackOverrun / DoubleDelete and,
// with #7 (FastFail), reproduce that sample's full WER trio across the C#/C++ boundary.
// (A plain access violation like #1 is NOT WER-class: the CLR intercepts it first.)

// 8. Stack-buffer overrun smashes the /GS stack cookie; on return __report_gsfailure
//    fail-fasts through WER (0xC0000409). Mirrors MyConsoleCrasher::StackOverrun, with two
//    adjustments for the managed P/Invoke context: optimization is pinned off so the frame
//    is laid out predictably (buffer adjacent to the cookie/return address — with the Release
//    optimizer the write missed the cookie entirely and the function returned cleanly), and
//    the overwrite is 256 bytes, not the console sample's 2000, so it corrupts the cookie +
//    return address without running the CLR's transition frames into a guard page (which
//    left the fail-fast un-bucketable). Requires /GS (default).
#pragma optimize("", off)
BSNATIVE_API void __cdecl bscrash_stack_overrun()
{
    char buffer[10];
    volatile char* p = buffer;
    p--;
    memset(const_cast<char*>(p), 'A', 256); // corrupt the /GS cookie + return address
}
#pragma optimize("", on)

// 9. Heap-metadata corruption via double free: the Windows heap fail-fasts through WER.
//    Mirrors MyConsoleCrasher::DoubleDelete. Reliable in Release; the debug CRT asserts.
BSNATIVE_API void __cdecl bscrash_double_delete()
{
    char* p = new char[100];
    delete[] p;
    delete[] p; // double free -> heap corruption -> __fastfail(HEAP_METADATA_CORRUPTION)
}

// 10. The same double free, made straight on the Windows process heap, which is what delete[]
//     does in Release. Bypassing the debug CRT heap makes it corrupt the heap (and fail-fast
//     through WER) in Debug builds too, instead of stopping at a debug-CRT assertion dialog.
BSNATIVE_API void __cdecl bscrash_heap_double_free()
{
    HANDLE heap = GetProcessHeap();
    void* p = HeapAlloc(heap, 0, 100);
    HeapFree(heap, 0, p);
    HeapFree(heap, 0, p); // double free -> heap corruption -> __fastfail(HEAP_METADATA_CORRUPTION)
}
