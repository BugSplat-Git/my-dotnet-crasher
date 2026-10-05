using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using BugSplatDotNet;

namespace MyDotNetWinUI3Crasher;

/// <summary>
/// Application entry point. Creates the single process-wide <see cref="BugSplat"/> instance
/// which is all the crash reporting a WinUI 3 app needs. Native hard faults and unhandled
/// managed exceptions, including ones on the XAML UI thread (which WinUI turns into a
/// fail-fast), are captured as a minidump by BugSplat.dll's handlers, installed by the
/// constructor; the fail-fasts go through WER, so BugSplatWer.dll must be registered (see
/// below). Handled exceptions are reported with <see cref="BugSplat.Post"/> from their catch.
/// </summary>
public partial class App : Application
{
    // Change these to your own BugSplat database, then watch crashes arrive at
    // https://app.bugsplat.com/v2/dashboard?database=<Database>
    public const string Database = "Fred";
    public const string Application = "MyDotNetWinUI3Crasher";
    public const string Version = "1.0.0";

    public static BugSplat BugSplat { get; private set; } = null!;

    private Window? _window;

    public App()
    {
        BugSplat = new BugSplat(Database, Application, Version)
        {
            User = "sample-user",
            Email = "sample-user@example.com",
            Description = "MyDotNetWinUI3Crasher sample crash",
        };
        BugSplat.SetAttribute("runtime", RuntimeInformation.FrameworkDescription);

        // A WinUI 3 app's crashes are captured through BugSplat's WER runtime-exception helper
        // (BugSplatWer.dll), which is registered only when this machine has an allowlist entry
        // naming BugSplatWer.dll under
        //   HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\RuntimeExceptionHelperModules
        // (created with administrator rights — see the BugSplat docs). Without it, crashes are
        // NOT reported, so warn the developer up front — mirrors the native MyWinUI3Crasher.
        if (!BugSplat.IsWerEnabled)
        {
            MessageBox(
                IntPtr.Zero,
                "BugSplat WER is not configured! Crashes will not be handled by BugSplat.\n\n" +
                "Register BugSplatWer.dll under HKLM\\SOFTWARE\\Microsoft\\Windows\\" +
                "Windows Error Reporting\\RuntimeExceptionHelperModules. See the BugSplat docs.",
                "Warning",
                MB_OK | MB_ICONWARNING);
        }

        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }

    // Win32 MessageBox for the startup WER warning (shown before any window exists, so a
    // XAML ContentDialog isn't available yet).
    private const uint MB_OK = 0x0, MB_ICONWARNING = 0x30;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);
}
