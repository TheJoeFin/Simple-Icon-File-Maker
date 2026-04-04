using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.ViewModels;
using Windows.ApplicationModel;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Simple_Icon_File_Maker;

public sealed partial class AboutPage : Page
{
    public AboutViewModel ViewModel { get; }

    public AboutPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<AboutViewModel>();
        VersionNumber.Text = GetAppDescription();
        InitializeThemeSelection();
        KeyDown += AboutPage_KeyDown;
    }

    private void AboutPage_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
            ViewModel.GoBack();
    }

    private void InitializeThemeSelection()
    {
        Microsoft.UI.Xaml.ElementTheme currentTheme = ViewModel.Theme;
        DefaultThemeButton.IsChecked = currentTheme == Microsoft.UI.Xaml.ElementTheme.Default;
        LightThemeButton.IsChecked = currentTheme == Microsoft.UI.Xaml.ElementTheme.Light;
        DarkThemeButton.IsChecked = currentTheme == Microsoft.UI.Xaml.ElementTheme.Dark;
    }

    private async void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton button &&
            Enum.TryParse(button.Tag?.ToString(), out Microsoft.UI.Xaml.ElementTheme theme))
        {
            await ViewModel.SwitchThemeAsync(theme);
        }
    }

    private static string GetAppDescription()
    {
        Package package = Package.Current;
        PackageId packageId = package.Id;
        PackageVersion version = packageId.Version;

        return $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    private async void ReviewBTN_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // NavigateUri="ms-windows-store://review/?ProductId=9NS1BM1FB99Z"
        await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NS1BM1FB99Z"));
    }

    private async void AppRatingControl_ValueChanged(RatingControl sender, object args)
    {
        double rating = sender.Value;
        sender.Value = -1;

        if (rating <= 0)
            return;

        if (rating >= 4)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NS1BM1FB99Z"));
            return;
        }

        TextBox feedbackBox = new()
        {
            PlaceholderText = "Describe the issue or suggestion...",
            AcceptsReturn = true,
            Height = 120,
            TextWrapping = TextWrapping.Wrap,
        };

        StackPanel content = new() { Spacing = 8 };
        content.Children.Add(new TextBlock
        {
            Text = "We're sorry to hear that! Please describe any issues or suggestions:",
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(feedbackBox);

        ContentDialog dialog = new()
        {
            Title = "Send Feedback",
            Content = content,
            PrimaryButtonText = "Send Email",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            string subject = Uri.EscapeDataString("Simple Icon File Maker Feedback");
            string body = Uri.EscapeDataString(feedbackBox.Text);
            await Windows.System.Launcher.LaunchUriAsync(
                new Uri($"mailto:joe@joefinapps.com?subject={subject}&body={body}"));
        }
    }
}
