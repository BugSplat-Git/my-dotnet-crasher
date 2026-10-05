using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyDotNetFrameworkWpfCrasher;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionChip.Text = "v" + App.Version;

        // Heap corruption fail-fasts the process before any in-process handler can run, so
        // BugSplat only sees it through WER. Without BugSplatWer.dll registered the card would
        // crash the app with no report, so disable it and say why.
        if (!App.BugSplat.IsWerEnabled)
        {
            HeapCorruptionCard.IsEnabled = false;
            HeapCorruptionCard.ToolTip =
                "BugSplat can only report heap corruption through Windows Error Reporting.\n" +
                "Register BugSplatWer.dll under HKLM\\SOFTWARE\\Microsoft\\Windows\\Windows Error Reporting\\" +
                "RuntimeExceptionHelperModules (see the README), then restart this app.";
        }
    }

    // --- Event cards ---

    // Unhandled managed exception on the dispatcher thread: BugSplat's native in-process filter
    // captures a minidump as the process goes down.
    private void CrashCard_Click(object sender, RoutedEventArgs e) => Crashers.UnhandledManagedException();

    // Non-fatal: report a caught exception as a minidump and keep running. Post must be called
    // inside the catch, while the throw-site frames are still on the stack.
    private void NonCrashErrorCard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Crashers.HandledException();
        }
        catch (Exception ex)
        {
            var posted = App.BugSplat.Post(ex);
            AddRecentActivity(posted ? "Non-crash error posted" : "Non-crash error could not be posted");
        }
    }

    private async void UserFeedbackCard_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FeedbackWindow { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        if (!string.IsNullOrWhiteSpace(dialog.UserName)) App.BugSplat.User = dialog.UserName;
        if (!string.IsNullOrWhiteSpace(dialog.UserEmail)) App.BugSplat.Email = dialog.UserEmail;

        // Read the dialog's text boxes here: WPF controls only work on the UI thread.
        var title = dialog.FeedbackTitle;
        var description = dialog.FeedbackDescription;

        // PostFeedback blocks on the monitor process; keep it off the UI thread.
        var result = await Task.Run(() => App.BugSplat.PostFeedback(title, description));

        AddRecentActivity(result.Success ? $"Feedback sent · crash {result.CrashId}" : "Feedback could not be sent");
        if (!result.Success)
        {
            MessageBox.Show(this, "Feedback could not be sent.", "Feedback", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var message = $"Thanks — your feedback reached BugSplat.\n\nCrash id: {result.CrashId}\n\nOpen it on the dashboard?";
        if (MessageBox.Show(this, message, "Feedback", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            OpenDashboard(result.CrashId);
    }

    // Freeze the UI thread; BugSplat's native hang detection reports it.
    private void HangCard_Click(object sender, RoutedEventArgs e) => Crashers.Hang();

    // Managed -> P/Invoke -> native access violation. BugSplat's native in-process filter
    // captures a minidump whose stack crosses the C#/C++ boundary.
    private void MixedModeCard_Click(object sender, RoutedEventArgs e) => NativeMethods.bscrash_access_violation();

    // Native double free -> heap corruption -> fail-fast. Reported only through WER (BugSplatWer.dll).
    private void HeapCorruptionCard_Click(object sender, RoutedEventArgs e) => NativeMethods.bscrash_heap_double_free();

    private void ViewDashboard_Click(object sender, RoutedEventArgs e) => OpenDashboard(0);

    private static void OpenDashboard(int crashId)
    {
        var url = crashId > 0
            ? $"https://app.bugsplat.com/v2/crash?database={App.Database}&id={crashId}"
            : $"https://app.bugsplat.com/v2/dashboard?database={App.Database}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private void AddRecentActivity(string title)
    {
        RecentActivityEmpty.Visibility = Visibility.Collapsed;
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        var when = new TextBlock { Text = "just now", FontSize = 12, Opacity = 0.6 };
        DockPanel.SetDock(when, Dock.Right);
        row.Children.Add(when);
        row.Children.Add(new TextBlock { Text = title, FontSize = 13 });
        RecentActivityPanel.Children.Insert(0, row);
    }
}
