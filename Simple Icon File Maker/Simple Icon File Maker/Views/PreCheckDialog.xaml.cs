using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_Icon_File_Maker;

public sealed partial class PreCheckDialog : ContentDialog
{
    public const string SkipPreCheckSettingKey = "SkipPreCheckDialog";

    // Set by caller before ShowModal
    public int TotalImageCount { get; set; }
    public ObservableCollection<FileGroupItem> FileGroups { get; } = [];

    // Read by caller after ShowModal returns
    public bool IsConfirmed { get; private set; }

    public PreCheckDialog()
    {
        InitializeComponent();
    }

    private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        SummaryTextBlock.Text =
            $"{TotalImageCount} image file{(TotalImageCount == 1 ? "" : "s")} found in this folder.";
    }

    private async void ContentDialog_PrimaryButtonClick(
        ContentDialog sender,
        ContentDialogButtonClickEventArgs args)
    {
        IsConfirmed = true;

        if (DontShowAgainCheckBox.IsChecked == true)
        {
            await App.GetService<ILocalSettingsService>()
                     .SaveSettingAsync(SkipPreCheckSettingKey, true);
        }
    }
}
