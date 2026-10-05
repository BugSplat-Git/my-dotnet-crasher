# MyDotNetWinUI3Crasher

A WinUI 3 (.NET 10) sample that reports crashes, non-fatal errors, and user feedback with the
BugSplat for .NET, from the [`BugSplat`](https://www.nuget.org/packages/BugSplat) NuGet package. It is the GUI companion
to the headless [`MyDotNetCrasher`](../MyDotNetCrasher) console sample and mirrors the C++
`MyWinUI3Crasher` sample from the native BugSplat SDK.

## Run

```
dotnet run -c Debug -p:Platform=x64
```

The app is **unpackaged** and **self-contained**, so it launches with no MSIX/certificate setup
and no separate Windows App SDK Runtime install. It is **x64 only**, because the
`MyDotNetCrasherNative.dll` its Mixed-Mode card calls is built for x64.

Set your database in `App.xaml.cs` (`App.Database`), then watch events arrive at
`https://app.bugsplat.com/v2/dashboard?database=<Database>`.

## Symbols

So crash stacks show file names and line numbers, each build uploads the exe, DLLs and PDBs to
BugSplat with `Scripts\SymbolUpload.ps1`, using `App.Database`, `App.Application` and
`App.Version` from `App.xaml.cs`. Create a Client ID and Client Secret for your database at
<https://app.bugsplat.com/v2/database/integrations#oauth>, then create `Scripts\env.ps1`:

```powershell
$BUGSPLAT_CLIENT_ID = "{{id}}"
$BUGSPLAT_CLIENT_SECRET = "{{secret}}"
```

or set the `BUGSPLAT_CLIENT_ID` and `BUGSPLAT_CLIENT_SECRET` environment variables. To build
without uploading, pass `/p:BugSplatSymbolUpload=false`.

## Configure WER (required for crash capture)

A WinUI 3 app's crashes are captured through BugSplat's WER runtime-exception helper
(`BugSplatWer.dll`), which the SDK registers only when this machine allowlists it under

```
HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules
```

Add a `REG_DWORD` value **named with the full path** to the `BugSplatWer.dll` sitting next to
the built exe (value data `0`). This needs administrator rights; see step 3 of the
[BugSplat for Windows (C++) guide](https://docs.bugsplat.com/integrations/desktop/cplusplus)
for details. If it isn't configured, `App` checks `BugSplat.IsWerEnabled` at startup and shows
a warning — crashes will not be reported until the key is present. (Most of the console sample's
modes are caught by BugSplat's application exception handler, but its fail-fast modes need the
same registration.)

## What it demonstrates

| Card | Path exercised |
| --- | --- |
| **Crash** | An unhandled managed exception: WinUI fail-fasts and BugSplat captures a minidump through WER. |
| **Non-Crash Error** | `BugSplat.Post(exception)` inside the `catch` writes a minidump of the caught exception and uploads it; the app keeps running. |
| **User Feedback** | `BugSplat.PostFeedback(...)`, returning a crash id + info URL. |
| **Hang** | Freezes the UI thread for good; the native SDK's hang detection reports it (within about 10 seconds) and then ends the process. |
| **Mixed-Mode Crash** | P/Invokes `MyDotNetCrasherNative.dll` into a native access violation; BugSplat symbolicates one unified C#/C++ stack. Build `MyDotNetWinUI3Crasher.sln`, which builds `..\MyDotNetCrasherNative` first. |

Every report is a minidump that BugSplat symbolicates from uploaded symbols. Crash reporting
is wired once, in `App`:

```csharp
BugSplat = new BugSplat(Database, Application, Version);
```

To report a handled exception, call `Post` from inside its `catch`, while the throw-site frames
are still on the stack:

```csharp
try { DoWork(); }
catch (Exception ex) { App.BugSplat.Post(ex); }
```

## Shipping the BugSplat native runtime

BugSplat captures crashes out-of-process: `BugSplat.dll` spawns `BugSplatMonitor.exe` from the
application's own directory, so `BugSplat.dll`, `BugSplatMonitor.exe`, `BugSplatRc.dll`, and
`BugSplatWer.dll` **must sit next to your executable at run time**.

The [`BugSplat`](https://www.nuget.org/packages/BugSplat) package adds them as `Content` items, so
they flow to `dotnet build`, `dotnet publish` (into `publish\`), and MSIX packaging with your app.
This sample adds `MyDotNetCrasherNative.dll` and its PDB the same way.

## Packaging as MSIX

To ship packaged instead of unpackaged, remove `<WindowsPackageType>None</WindowsPackageType>`
and `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` from the `.csproj` and add a
Windows Application Packaging Project. The BugSplat runtime files are included in the package
unchanged; `BugSplatMonitor.exe` is packaged as a plain data file (do not list it as an
`<Executable>` in the manifest).
