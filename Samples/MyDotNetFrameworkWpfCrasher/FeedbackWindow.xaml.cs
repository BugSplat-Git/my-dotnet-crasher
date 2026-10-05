using System.Windows;

namespace MyDotNetFrameworkWpfCrasher;

/// <summary>Collects a feedback title, description and optional name/email.</summary>
public partial class FeedbackWindow : Window
{
    public FeedbackWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => TitleBox.Focus();
    }

    public string FeedbackTitle => string.IsNullOrWhiteSpace(TitleBox.Text) ? "Feedback" : TitleBox.Text;
    public string FeedbackDescription => DescriptionBox.Text;
    public string UserName => NameBox.Text;
    public string UserEmail => EmailBox.Text;

    private void Send_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
