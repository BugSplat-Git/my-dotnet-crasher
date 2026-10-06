# MyDotNetCrasher

A headless console (.NET 10) sample that reports crashes, non-fatal errors, and user feedback
with BugSplat for .NET, from the [`BugSplat`](https://www.nuget.org/packages/BugSplat) NuGet package. It is the
terminal companion to the GUI [`MyDotNetWinUI3Crasher`](../MyDotNetWinUI3Crasher) and mirrors the
C++ `MyConsoleCrasher` sample from the native BugSplat SDK.

## Run

```
dotnet run -- <mode>
```

Set your database at the top of `Program.cs` (`new BugSplat("Fred", ...)`), then watch events
arrive at `https://app.bugsplat.com/v2/dashboard?database=<Database>`. It is **x64 only**,
because the `MyDotNetCrasherNative.dll` its mixed-mode modes call is built for x64.

## Symbols

So crash stacks show file names and line numbers, each build uploads the exe, DLLs and PDBs to
BugSplat with `Scripts\SymbolUpload.ps1`, using the database, application and version from the
`new BugSplat(...)` call in `Program.cs`. Create a Client ID and Client Secret for your database at
<https://app.bugsplat.com/v2/database/integrations#oauth>, then create `Scripts\env.ps1`:

```powershell
$BUGSPLAT_CLIENT_ID = "{{id}}"
$BUGSPLAT_CLIENT_SECRET = "{{secret}}"
```

or set the `BUGSPLAT_CLIENT_ID` and `BUGSPLAT_CLIENT_SECRET` environment variables. To build
without uploading, pass `/p:BugSplatSymbolUpload=false`.

## What it demonstrates

Every report is a minidump that BugSplat symbolicates from uploaded symbols, so managed frames
show file names and line numbers without shipping PDBs.

| Mode | Path exercised |
| --- | --- |
| `managed` | Unhandled C# exception → BugSplat's application exception handler captures a minidump. |
| `handled` | A caught C# exception reported with `BugSplat.Post(ex)` from inside the `catch` → a minidump with the throw-site frames; the process keeps running. |
| `native` | A hardware access violation the CLR never surfaces → BugSplat's application exception handler captures a minidump. |
| `feedback` | `BugSplat.PostFeedback(...)`, returning a report id + info URL (no crash). |
| `deep` / `generic` / `lambda` / `async` | Managed faults that fault deep / from a closed generic / a LINQ lambda / an async state machine — fixtures for the managed stack walk + method naming. |

### Mixed-mode C#/C++ (P/Invoke into `MyDotNetCrasherNative.dll`)

These fault across the managed→native boundary, so the crash stack is one unified C#/C++ trace —
the cases BugSplat and [`bugsplat-cdb`](https://github.com/BugSplat-Git/bugsplat-cdb)
symbolicate as a single interleaved stack:

| Mode | Shape |
| --- | --- |
| `native-av` | Managed → native access violation (native frame on top). |
| `native-deep` | Several native C++ frames beneath the managed transition. |
| `native-callback` | Managed → native → managed callback that throws (M/N/M interleave). |
| `cpp-throw` | Uncaught C++ exception in native code. |
| `native-so` | Cross-boundary stack overflow (native ↔ managed mutual recursion). |
| `native-thread` | A native background thread with no managed frames faults. |

**WER-class** mixed-mode modes — fail-fasts the OS routes straight through WerFault, bypassing
the CLR and BugSplat's application exception handler, so they are captured **only** by the allowlisted
`BugSplatWer.dll` runtime-exception helper (see *WER* below). They mirror `MyConsoleCrasher`'s
WER trio:

| Mode | Shape |
| --- | --- |
| `native-fastfail` | `__fastfail` from native code (`0xC0000409`). |
| `native-overrun` | `/GS` stack-cookie smash (`0xC0000409`). |
| `native-double-delete` | Heap-metadata corruption via double free (`0xC0000374`). |

The mixed-mode modes require `MyDotNetCrasherNative.dll`: build
[`..\MyDotNetCrasherNative`](../MyDotNetCrasherNative) (x64, MSBuild) first — this project copies
it next to the exe.

For the opposite direction — a native C++ program that hosts the runtime and crashes in C# code it
called — see [`MyDotNetFrameworkHostCrasher`](../MyDotNetFrameworkHostCrasher), which does it with
the .NET Framework.

## WER (for the fail-fast modes)

The `native-fastfail` / `native-overrun` / `native-double-delete` modes are captured through
BugSplat's WER runtime-exception helper (`BugSplatWer.dll`), which the SDK registers only when
the machine allowlists it under
`HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules` (a
`REG_DWORD` named with the full path to `BugSplatWer.dll`, created with administrator rights —
see step 3 of the [BugSplat for Windows (C++) guide](https://docs.bugsplat.com/integrations/desktop/cplusplus)).
The other modes use BugSplat's application exception handler and need no such setup.

## Shipping the BugSplat native runtime

BugSplat captures crashes out-of-process: `BugSplat.dll` spawns `BugSplatMonitor.exe` from the
application's own directory, so `BugSplat.dll`, `BugSplatMonitor.exe`, `BugSplatReporter.exe` (the crash dialog), and
`BugSplatWer.dll` **must sit next to your executable at run time**. The `BugSplat` package
copies them, with their PDBs, into the build output, and the binaries into the `dotnet publish`
output. This project adds `MyDotNetCrasherNative.dll` and its PDB the same way.
