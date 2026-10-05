# MyDotNetCrasherNative

The native C++ crash surface for the .NET samples — an **x64** DLL they P/Invoke to produce
**mixed-mode C#/C++ crashes**: the mixed-mode modes of [`MyDotNetCrasher`](../MyDotNetCrasher),
and the Mixed-Mode cards of [`MyDotNetWinUI3Crasher`](../MyDotNetWinUI3Crasher) and
[`MyDotNetFrameworkWpfCrasher`](../MyDotNetFrameworkWpfCrasher) (plus the WPF sample's Heap
Corruption card). Each fault happens in native code called from
managed code, so the resulting minidump has a call stack that crosses the C#/C++ boundary — the
cases BugSplat and [`bugsplat-cdb`](https://github.com/BugSplat-Git/bugsplat-cdb) symbolicate as
one unified, interleaved managed+native stack.

## Exports

Flat C ABI (`extern "C"`, `__cdecl`) so C# can P/Invoke with `CallingConvention.Cdecl`:

| Export | Crash |
| --- | --- |
| `bscrash_access_violation` | Write to an unmapped address (native frame on top of the managed transition). |
| `bscrash_deep` | Several distinct native frames, then fault. |
| `bscrash_invoke_callback` | Calls a managed callback that throws (managed → native → managed). |
| `bscrash_cpp_throw` | Uncaught C++ exception (`std::terminate`). |
| `bscrash_recurse_via_callback` | Native ↔ managed mutual recursion → stack overflow. |
| `bscrash_background_thread` | A native background thread with no managed frames faults. |
| `bscrash_fastfail` | `__fastfail` — a fail-fast that routes through WER. |
| `bscrash_stack_overrun` | `/GS` stack-cookie smash — fail-fast through WER. |
| `bscrash_double_delete` | Heap-metadata corruption via double free — fail-fast through WER. Release builds only: the debug CRT asserts first. |
| `bscrash_heap_double_free` | The same double free on the Windows process heap directly, so it corrupts the heap in Debug builds too — fail-fast through WER. |

The last four are **WER-class**: they bypass every SEH/VEH handler (including the CLR's) and are
captured only by BugSplat's allowlisted `BugSplatWer.dll` helper. See the
[`MyDotNetCrasher`](../MyDotNetCrasher) README for the mode names that drive each export, and the
WER section of the [`MyDotNetFrameworkWpfCrasher`](../MyDotNetFrameworkWpfCrasher) README for the
registry setup.

## Build

```
msbuild MyDotNetCrasherNative.vcxproj -p:Configuration=Release -p:Platform=x64
```

x64 only, built with Visual Studio's default C++ toolset, PDB emitted in both configs (BugSplat/bugsplat-cdb need it to
symbolicate the native half of the stack). A .NET SDK project can't build a `.vcxproj` under
`dotnet build`, so the .NET samples consume this DLL **prebuilt** — build it first, then the
samples copy `MyDotNetCrasherNative.dll` + `.pdb` next to their exe. Each sample's solution
builds it first.
