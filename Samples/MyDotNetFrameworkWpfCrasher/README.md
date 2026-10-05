# MyDotNetFrameworkWpfCrasher

A WPF (.NET Framework 4.7.2) sample that reports crashes, non-fatal errors, hangs, and user
feedback with BugSplat for .NET, from the [`BugSplat`](https://www.nuget.org/packages/BugSplat) NuGet package. It is
the .NET Framework counterpart of the .NET 10 WinUI 3
[`MyDotNetWinUI3Crasher`](../MyDotNetWinUI3Crasher).

## Run

```
dotnet run -c Debug -p:Platform=x64
```

Set your database in `App.xaml.cs` (`App.Database`), then watch events arrive at
`https://app.bugsplat.com/v2/dashboard?database=<Database>`.

The app is **x64 only**, because the `MyDotNetCrasherNative.dll` its Mixed-Mode card calls is
built for x64. A .NET Framework exe defaults to AnyCPU with *Prefer 32-bit*, which runs as a
32-bit process, so the project sets `<PlatformTarget>x64</PlatformTarget>`.

The Mixed-Mode card needs `MyDotNetCrasherNative.dll` from
[`..\MyDotNetCrasherNative`](../MyDotNetCrasherNative). Build `MyDotNetFrameworkWpfCrasher.sln`
for x64, which builds it first; `dotnet run` alone needs it built for the same configuration.

## Symbols

So crash stacks show file names and line numbers, each build uploads the exe, DLLs and PDBs to
BugSplat with `Scripts\SymbolUpload.ps1`. Create a Client ID and Client Secret for your database at
<https://app.bugsplat.com/v2/database/integrations#oauth>, then create `Scripts\env.ps1`:

```powershell
$BUGSPLAT_CLIENT_ID = "{{id}}"
$BUGSPLAT_CLIENT_SECRET = "{{secret}}"
```

The script also reads `BUGSPLAT_CLIENT_ID` and `BUGSPLAT_CLIENT_SECRET` from the environment. To
build without uploading, pass `/p:BugSplatSymbolUpload=false`.

## What it demonstrates

| Card | Path exercised |
| --- | --- |
| **Crash** | An unhandled managed exception on the UI thread; BugSplat's application exception handler captures a minidump. |
| **Non-Crash Error** | `BugSplat.Post(exception)` inside the `catch` writes a minidump of the caught exception and uploads it; the app keeps running. |
| **User Feedback** | `BugSplat.PostFeedback(...)`, returning a crash id + info URL. |
| **Hang** | Freezes the UI thread for good; the native SDK's hang detection reports it (within about 10 seconds) and then ends the process. |
| **Mixed-Mode Crash** | P/Invokes `MyDotNetCrasherNative.dll` into a native access violation; BugSplat symbolicates one unified C#/C++ stack. |
| **Heap Corruption** | A C++ library (`MyDotNetCrasherNative.dll`) frees the same heap block twice; Windows fail-fasts the process. Captured **only through WER**; the card is disabled until `BugSplatWer.dll` is registered (see below). |

Every report is a minidump, symbolicated by BugSplat from the symbols the build uploads, so
managed frames show file names and line numbers without shipping PDBs. Crash reporting is
wired once, in `App`:

```csharp
BugSplat = new BugSplat(Database, AppName, Version);
```

No WPF-specific handler is needed: an exception on the dispatcher thread that nothing handles
reaches the same application exception handler. To report a handled exception, call `Post` from inside its
`catch`, while the throw-site frames are still on the stack:

```csharp
try { DoWork(); }
catch (Exception ex) { App.BugSplat.Post(ex); }
```

## WER (for the Heap Corruption card)

On .NET Framework, unhandled managed exceptions and access violations reach BugSplat's
application exception handler, so every card except Heap Corruption works without any setup.
Heap corruption is different: when Windows detects it, it fail-fasts the process straight
through Windows Error Reporting, bypassing the application's exception handlers, so BugSplat only sees it
via its runtime-exception helper `BugSplatWer.dll`. The same goes for other fail-fasts from
native code, such as `__fastfail` or a `/GS` stack-cookie failure. WER loads the helper only
when its full path is allowlisted under

```
HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules
```

as a `REG_DWORD` value named with the path to the `BugSplatWer.dll` next to the built exe (value
data `0`). From an elevated prompt:

```
reg add "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules" /v "<path to exe folder>\BugSplatWer.dll" /t REG_DWORD /d 0 /f
```

The app checks `BugSplat.IsWerEnabled` at startup and disables the Heap Corruption card, with a
tooltip explaining why, until the entry exists. Restart the app after adding it. See step 3 of
the [BugSplat for Windows (C++) guide](https://docs.bugsplat.com/integrations/desktop/cplusplus)
for details.

A stack overflow in managed code is not covered, even with WER registered: the .NET Framework
reports it through its own `CLR20r3` WER event, which doesn't call runtime-exception helpers.

## Adding BugSplat to a .NET Framework app

- Add the [`BugSplat`](https://www.nuget.org/packages/BugSplat) NuGet package.
- `BugSplat.dll`, `BugSplatMonitor.exe`, `BugSplatRc.dll`, and `BugSplatWer.dll` **must sit next
  to your executable at run time**: `BugSplat.dll` spawns `BugSplatMonitor.exe` from the
  application's own directory. The package copies them into your output for the architecture
  your app runs as.
