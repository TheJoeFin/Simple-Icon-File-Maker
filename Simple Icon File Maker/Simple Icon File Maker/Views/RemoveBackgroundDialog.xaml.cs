using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_Icon_File_Maker.Helpers;

namespace Simple_Icon_File_Maker;

public sealed partial class RemoveBackgroundDialog : ContentDialog
{
    public string? ResultImagePath { get; private set; }

    private readonly string _imagePath;
    private string? _pendingResultPath;

    public RemoveBackgroundDialog(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ResultImagePath = _pendingResultPath;
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        BeforeImage.Source = new BitmapImage(new Uri(_imagePath));

        bool isAvailable = await BackgroundRemoverHelper.IsAvailableAsync();
        if (!isAvailable)
        {
            StatusInfoBar.Title = "Not Available";
            StatusInfoBar.Message = "The AI background removal model is not available on this device. This feature requires a Copilot+ PC with the latest Windows updates.";
            StatusInfoBar.Severity = InfoBarSeverity.Warning;
            StatusInfoBar.IsOpen = true;
            ProcessingRing.IsActive = false;
            return;
        }

        try
        {
            string resultPath = await BackgroundRemoverHelper.RemoveBackgroundAsync(_imagePath);
            _pendingResultPath = resultPath;

            AfterImage.Source = new BitmapImage(new Uri(resultPath));

            IsPrimaryButtonEnabled = true;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Title = "Error";
            StatusInfoBar.Message = $"Failed to remove background: {ex.Message}";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            ProcessingRing.IsActive = false;
        }
    }
}
