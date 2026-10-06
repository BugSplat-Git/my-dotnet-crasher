[![bugsplat-github-banner-basic-outline](https://user-images.githubusercontent.com/20464226/149019306-3186103c-5315-4dad-a499-4fd1df408475.png)](https://bugsplat.com)
<br/>

# <div align="center">BugSplat</div>

### **<div align="center">Crash and error reporting built for busy developers.</div>**

<div align="center">
    <a href="https://bsky.app/profile/bugsplatco.bsky.social"><img alt="Follow @bugsplatco on Bluesky" src="https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fpublic.api.bsky.app%2Fxrpc%2Fapp.bsky.actor.getProfile%2F%3Factor%3Dbugsplatco.bsky.social&query=%24.followersCount&style=social&logo=bluesky&label=Follow%20%40bugsplatco.bsky.social"></a>
    <a href="https://discord.gg/bugsplat"><img alt="Join BugSplat on Discord" src="https://img.shields.io/discord/664965194799251487?label=Join%20Discord&logo=Discord&style=social"></a>
</div>

<br/>

# my-dotnet-crasher

Sample applications for [BugSplat for .NET](https://docs.bugsplat.com/introduction/getting-started/integrations/desktop/bugsplat-for-dot-net), which reports crashes, hangs, handled exceptions, and user feedback from .NET Framework and .NET 10 applications on Windows. Each sample installs it from the [`BugSplat`](https://www.nuget.org/packages/BugSplat) NuGet package.

| Sample | Runtime | What it shows |
| --- | --- | --- |
| [MyDotNetCrasher](Samples/MyDotNetCrasher) | .NET 10 console | A mode for every kind of crash, including mixed C#/C++ crashes |
| [MyDotNetWinUI3Crasher](Samples/MyDotNetWinUI3Crasher) | .NET 10, WinUI 3 | A button for each kind of report, and a custom crash dialog theme |
| [MyDotNetFrameworkWpfCrasher](Samples/MyDotNetFrameworkWpfCrasher) | .NET Framework 4.7.2, WPF | A button for each kind of report |

The mixed C#/C++ crashes call into a small C++ library, [MyDotNetCrasherNative](Samples/MyDotNetCrasherNative), which each sample's solution builds first.

## Prerequisites

- Windows
- Visual Studio 2022 or later, with the **.NET desktop development** and **Desktop development with C++** workloads, plus **WinUI application development** for the WinUI 3 sample
- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [BugSplat](https://www.bugsplat.com) account

## Getting Started

1. Clone this repository:

   ```
   git clone https://github.com/BugSplat-Git/my-dotnet-crasher.git
   ```

2. Set your BugSplat database in the sample you want to run: `Program.cs` for MyDotNetCrasher, or `App.Database` in `App.xaml.cs` for the WinUI 3 and WPF samples.
3. Create a Client ID and Client Secret for your database on the [Integrations](https://app.bugsplat.com/v2/database/integrations#oauth) page, and put them in the sample's `Scripts\env.ps1`:

   ```powershell
   $BUGSPLAT_CLIENT_ID = "your-client-id"
   $BUGSPLAT_CLIENT_SECRET = "your-client-secret"
   ```

   or set the `BUGSPLAT_CLIENT_ID` and `BUGSPLAT_CLIENT_SECRET` environment variables. Each build uploads the sample's symbols with [symbol-upload](https://github.com/BugSplat-Git/symbol-upload), which `Tools\Get-SymbolUpload.ps1` downloads on the first build, so crash reports show function names, file names, and line numbers. To build without uploading, pass `/p:BugSplatSymbolUpload=false`.
4. Open the sample's solution (`MyDotNetCrasher.sln`, `MyDotNetWinUI3Crasher.sln`, or `MyDotNetFrameworkWpfCrasher.sln`) in Visual Studio and build it for **x64**.
5. Run the sample outside the Visual Studio debugger (Ctrl+F5), which would otherwise intercept the crashes BugSplat reports.
6. Open the [Crashes](https://app.bugsplat.com/v2/crashes) page and click a crash's ID to see its symbolicated call stack.

Each sample's README describes its crashes and buttons in detail.

## Windows Error Reporting

Some crashes bypass the application's exception handlers and go straight to Windows Error Reporting: heap corruption, `__fastfail`, `/GS` failures, and every crash in a WinUI 3 app. BugSplat captures them with `BugSplatWer.dll`, which Windows loads only when its path is in the registry. **The WinUI 3 sample requires it.** From an elevated prompt:

```
reg add "HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules" /v "<path to the sample's output folder>\BugSplatWer.dll" /t REG_DWORD /d 0 /f
```

## Learn More

- [BugSplat for .NET](https://docs.bugsplat.com/introduction/getting-started/integrations/desktop/bugsplat-for-dot-net)
- [The `BugSplat` NuGet package](https://www.nuget.org/packages/BugSplat)
- [Uploading symbols](https://docs.bugsplat.com/introduction/development/working-with-symbol-files)
