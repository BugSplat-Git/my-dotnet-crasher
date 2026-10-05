using System.Runtime.InteropServices;
using System.Windows;
using BugSplatDotNet;

namespace MyDotNetFrameworkWpfCrasher;

/// <summary>
/// Application entry point. Creates the single process-wide <see cref="BugSplat"/> instance,
/// which is all the crash reporting a .NET Framework WPF app needs: unhandled managed
/// exceptions (including ones on the dispatcher thread) and native hard faults reach
/// BugSplat.dll's in-process exception filter, installed by the constructor, which captures a
/// minidump. Handled exceptions are reported with <see cref="BugSplat.Post"/> from their catch.
/// </summary>
public partial class App : Application
{
    // Change these to your own BugSplat database, then watch crashes arrive at
    // https://app.bugsplat.com/v2/dashboard?database=<Database>
    public const string Database = "Fred";
    public const string AppName = "MyDotNetFrameworkWpfCrasher";
    public const string Version = "1.0.0";

    public static BugSplat BugSplat { get; private set; } = null!;

    public App()
    {
        BugSplat = new BugSplat(Database, AppName, Version)
        {
            User = "sample-user",
            Email = "sample-user@example.com",
            Description = "MyDotNetFrameworkWpfCrasher sample crash",
        };
        BugSplat.SetAttribute("runtime", RuntimeInformation.FrameworkDescription);
    }
}
