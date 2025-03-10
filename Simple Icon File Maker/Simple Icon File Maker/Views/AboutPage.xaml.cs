﻿using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.ViewModels;
using Windows.ApplicationModel;

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
        bool result = await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?ProductId=9NS1BM1FB99Z"));
    }
}
