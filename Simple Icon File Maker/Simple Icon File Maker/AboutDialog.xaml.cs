using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using Windows.ApplicationModel;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Simple_Icon_File_Maker;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class AboutDialog : ContentDialog
{
    public AboutDialog()
    {
        InitializeComponent();

        VersionNumber.Text = GetAppDescription();
    }

    private string GetAppDescription()
    {
        Package package = Package.Current;
        PackageId packageId = package.Id;
        PackageVersion version = packageId.Version;

        return $"{package.DisplayName} - {version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
    }

    private async void WindowsAppSDKBTN_Click(object sender, RoutedEventArgs e)
    {
        Uri imageMagickNetGH = new("https://github.com/microsoft/WindowsAppSDK");
        _ = await Launcher.LaunchUriAsync(imageMagickNetGH);
    }

    private async void WinUIBTN_Click(object sender, RoutedEventArgs e)
    {
        Uri imageMagickNetGH = new("https://github.com/Microsoft/microsoft-ui-xaml");
        _ = await Launcher.LaunchUriAsync(imageMagickNetGH);
    }

    private async void MagickDotNetBTN_Click(object sender, RoutedEventArgs e)
    {
        Uri imageMagickNetGH = new("https://github.com/dlemstra/Magick.NET");
        _ = await Launcher.LaunchUriAsync(imageMagickNetGH);
    }

    private async void TheJoeFinBTN_Click(object sender, RoutedEventArgs e)
    {
        Uri imageMagickNetGH = new("https://github.com/TheJoeFin");
        _ = await Launcher.LaunchUriAsync(imageMagickNetGH);
    }

    private async void SourceBTN_Click(object sender, RoutedEventArgs e)
    {
        Uri imageMagickNetGH = new("https://github.com/TheJoeFin/Simple-Icon-File-Maker");
        _ = await Launcher.LaunchUriAsync(imageMagickNetGH);
    }
}
