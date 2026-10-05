using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace MyDotNetWinUI3Crasher;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionChip.Text = "v" + App.Version;
    }

    // --- Event cards ---

    // Unhandled managed exception: WinUI fail-fasts and BugSplat captures a minidump through
    // WER. (Swap in Crashers.NativeAccessViolation() for a pure hardware fault.)
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
            AddRecentActivity(posted ? "Non-crash error posted" : "Non-crash error could not be posted", "just now");
        }
    }

    private async void UserFeedbackCard_Click(object sender, RoutedEventArgs e) => await ShowFeedbackDialogAsync();

    // Freeze the UI thread; BugSplat's native hang detection reports it.
    private void HangCard_Click(object sender, RoutedEventArgs e) => Crashers.Hang();

    // Managed -> P/Invoke -> native access violation. A native fault (not a managed
    // exception), so it bypasses App.UnhandledException and BugSplat's native handler
    // captures a minidump whose stack crosses the C#/C++ boundary.
    private void MixedModeCard_Click(object sender, RoutedEventArgs e) => NativeMethods.bscrash_access_violation();

    private void ViewDashboard_Click(object sender, RoutedEventArgs e) => OpenDashboard(0);

    private void OpenDashboard(int crashId)
    {
        var url = crashId > 0
            ? $"https://app.bugsplat.com/v2/crash?database={App.Database}&id={crashId}"
            : $"https://app.bugsplat.com/v2/dashboard?database={App.Database}";
        _ = Launcher.LaunchUriAsync(new Uri(url));
    }

    private void AddRecentActivity(string title, string time)
    {
        RecentActivityEmpty.Visibility = Visibility.Collapsed;
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBlock { Text = title, FontSize = 13 };
        name.SetValue(Grid.ColumnProperty, 0);
        var when = new TextBlock { Text = time, FontSize = 12, Opacity = 0.6 };
        when.SetValue(Grid.ColumnProperty, 1);
        row.Children.Add(name);
        row.Children.Add(when);
        RecentActivityPanel.Children.Insert(0, row);
    }

    // --- Feedback dialog ---

    private async Task ShowFeedbackDialogAsync()
    {
        var title = new TextBox { Header = "Title", Style = (Style)Application.Current.Resources["BoxedTextBoxStyle"], PlaceholderText = "Short summary" };
        var description = new TextBox { Header = "What happened?", Style = (Style)Application.Current.Resources["BoxedTextBoxStyle"], PlaceholderText = "Tell us more", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 96 };
        var name = new TextBox { Header = "Name", Style = (Style)Application.Current.Resources["BoxedTextBoxStyle"], PlaceholderText = "optional" };
        var email = new TextBox { Header = "Email", Style = (Style)Application.Current.Resources["BoxedTextBoxStyle"], PlaceholderText = "optional" };

        var panel = new StackPanel { Spacing = 12, MinWidth = 420 };
        panel.Children.Add(title);
        panel.Children.Add(description);
        panel.Children.Add(name);
        panel.Children.Add(email);

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Send feedback",
            Content = panel,
            PrimaryButtonText = "Send",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            return;

        if (!string.IsNullOrWhiteSpace(name.Text)) App.BugSplat.User = name.Text;
        if (!string.IsNullOrWhiteSpace(email.Text)) App.BugSplat.Email = email.Text;

        // Read the text boxes here: XAML controls only work on the UI thread.
        var feedbackTitle = string.IsNullOrWhiteSpace(title.Text) ? "Feedback" : title.Text;
        var feedbackDescription = description.Text;

        // PostFeedback blocks on the monitor process; keep it off the UI thread.
        var result = await Task.Run(() => App.BugSplat.PostFeedback(feedbackTitle, feedbackDescription));

        AddRecentActivity(result.Success ? $"Feedback sent · crash {result.CrashId}" : "Feedback could not be sent", "just now");
        await ShowResultAsync(result);
    }

    private async Task ShowResultAsync(BugSplatDotNet.FeedbackResult result)
    {
        var panel = new StackPanel { Spacing = 8, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = result.Success ? "Thanks — your feedback reached BugSplat." : "Feedback could not be sent.",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (result.CrashId > 0)
            panel.Children.Add(new TextBlock { Text = $"Crash id: {result.CrashId}", FontSize = 13, Opacity = 0.7 });
        if (!string.IsNullOrEmpty(result.InfoUrl))
            panel.Children.Add(new HyperlinkButton { Content = result.InfoUrl, NavigateUri = new Uri(result.InfoUrl), Padding = new Thickness(0) });

        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Feedback",
            Content = panel,
            PrimaryButtonText = result.CrashId > 0 ? "View on dashboard" : "Close",
            CloseButtonText = result.CrashId > 0 ? "Close" : null,
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && result.CrashId > 0)
            OpenDashboard(result.CrashId);
    }
}
