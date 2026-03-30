using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker;

public sealed partial class CropImageDialog : ContentDialog
{
    public string? ResultImagePath { get; private set; }

    private readonly string _imagePath;

    public CropImageDialog(string imagePath)
    {
        InitializeComponent();
        _imagePath = imagePath;
        PrimaryButtonClick += OnPrimaryButtonClick;
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            StorageFile file = await StorageFile.GetFileFromPathAsync(_imagePath);
            await ImageCropperControl.LoadImageFromFile(file);

            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ImageCropperControl.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = true;
        }
        catch (Exception ex)
        {
            LoadingRing.IsActive = false;
            LoadingRing.Visibility = Visibility.Collapsed;
            ErrorInfoBar.Title = "Error";
            ErrorInfoBar.Message = $"Failed to load image: {ex.Message}";
            ErrorInfoBar.IsOpen = true;
        }
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();

        try
        {
            StorageFolder cacheFolder = ApplicationData.Current.LocalCacheFolder;
            string fileName = Path.GetFileNameWithoutExtension(_imagePath);
            string newFileName = $"{fileName}_crop.png";

            StorageFile outputFile = await cacheFolder.CreateFileAsync(newFileName, CreationCollisionOption.ReplaceExisting);

            using IRandomAccessStream stream = await outputFile.OpenAsync(FileAccessMode.ReadWrite);
            await ImageCropperControl.SaveAsync(stream, CommunityToolkit.WinUI.Controls.BitmapFileFormat.Png);

            ResultImagePath = outputFile.Path;
        }
        catch (Exception ex)
        {
            ErrorInfoBar.Title = "Error";
            ErrorInfoBar.Message = $"Failed to save cropped image: {ex.Message}";
            ErrorInfoBar.IsOpen = true;
            args.Cancel = true;
        }
        finally
        {
            deferral.Complete();
        }
    }
}
