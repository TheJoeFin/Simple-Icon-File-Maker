using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_Icon_File_Maker.Helpers;

namespace Simple_Icon_File_Maker;

public sealed partial class RemoveBackgroundDialog : ContentDialog
{
    public string? ResultImagePath { get; private set; }

    private readonly string _imagePath;

    public RemoveBackgroundDialog(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            BeforeImage.Source = new BitmapImage(new Uri(_imagePath));

            bool available = await BackgroundRemoverHelper.IsAvailableAsync();
            if (!available)
            {
                StatusInfoBar.Title = "Not Available";
                StatusInfoBar.Message = "The AI background removal model is not available on this device. This feature requires a Copilot+ PC with the latest Windows updates.";
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                StatusInfoBar.IsOpen = true;
                ProcessingRing.IsActive = false;
                return;
            }

            string resultPath = await BackgroundRemoverHelper.RemoveBackgroundAsync(_imagePath);
            ResultImagePath = resultPath;

            AfterImage.Source = new BitmapImage(new Uri(resultPath));

            ProcessingRing.IsActive = false;
            IsPrimaryButtonEnabled = true;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Title = "Error";
            StatusInfoBar.Message = $"Failed to remove background: {ex.Message}";
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.IsOpen = true;
            ProcessingRing.IsActive = false;
        }
    }
}
