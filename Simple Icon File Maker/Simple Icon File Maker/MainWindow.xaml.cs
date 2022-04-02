using ImageMagick;
using ImageMagick.ImageOptimizers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Simple_Icon_File_Maker;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private string ImagePath = "";

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void OpenBTN_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".png");

        Window window = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFile file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        ImagePath = file.Path;

        // Ensure the stream is disposed once the image is loaded
        using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
        BitmapImage bitmapImage = new();
        // Decode pixel sizes are optional
        // It's generally a good optimisation to decode to match the size you'll display
        // bitmapImage.DecodePixelHeight = decodePixelHeight;
        // bitmapImage.DecodePixelWidth = decodePixelWidth;

        await bitmapImage.SetSourceAsync(fileStream);
        MainImage.Source = bitmapImage;
        await SourceImageUpdated();
    }

    private async Task SourceImageUpdated()
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        await GenerateIcons(sf.Path, true);
        SaveBTN.IsEnabled = true;
        dropHere.Visibility = Visibility.Collapsed;
    }

    private async Task GenerateIcons(string path, bool updatePreviews = false)
    {
        string openedPath = Path.GetDirectoryName(path);
        string name = Path.GetFileNameWithoutExtension(path);
        string iconRootString = Path.Combine(openedPath, "temp");
        string croppedImagePath = Path.Combine(iconRootString, $"{name}Cropped.png");
        string iconOutputString = Path.Combine(iconRootString, $"{name}.ico");
        if (Directory.Exists(iconRootString) == false)
            Directory.CreateDirectory(iconRootString);

        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();

        using IMagickImage<ushort> firstPassimage = await imgFactory.CreateAsync(ImagePath);
        IMagickGeometry size = geoFactory.Create(
            0,
            0,
            (int)MainImage.ActualWidth,
            (int)MainImage.ActualHeight);
        size.IgnoreAspectRatio = false;

        firstPassimage.Crop(size);
        await firstPassimage.WriteAsync(croppedImagePath);

        using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);
        List<int> intList = new() { 256, 128, 64, 32, 16 };

        MagickImageCollection collection = new();
        Dictionary<int,string> imagePaths = new();

        foreach (int sideLength in intList)
        {
            if (MainImage.ActualWidth < sideLength || MainImage.ActualHeight < sideLength)
                continue;

            IMagickGeometry iconSize = geoFactory.Create(sideLength, sideLength);
            iconSize.IgnoreAspectRatio = false;

            image.Resize(iconSize);
            string iconPath = $"{iconRootString}\\Image{sideLength}.png";
            await image.WriteAsync(iconPath);

            collection.Add(iconPath);
            imagePaths.Add(sideLength,iconPath);
        }

        if (updatePreviews == true)
            await UpdatePreviewsAsync(imagePaths);

        await collection.WriteAsync(iconOutputString);

        IcoOptimizer icoOpti = new()
        {
            OptimalCompression = true
        };
        icoOpti.Compress(iconOutputString);
    }

    private async Task UpdatePreviewsAsync(Dictionary<int,string> imagePaths)
    {
        foreach (var pair in imagePaths)
        {
            if (pair.Value is not string imagePath)
                return;

            int sideLength = pair.Key;

            StorageFile imageSF = await StorageFile.GetFileFromPathAsync(imagePath);
            using IRandomAccessStream fileStream = await imageSF.OpenAsync(FileAccessMode.Read);
            BitmapImage bitmapImage = new()
            {
                DecodePixelHeight = sideLength,
                DecodePixelWidth = sideLength
            };

            await bitmapImage.SetSourceAsync(fileStream);
            _ = sideLength switch
            {
                256 => OutputImage256.Source = bitmapImage,
                128 => OutputImage128.Source = bitmapImage,
                64 => OutputImage64.Source = bitmapImage,
                32 => OutputImage32.Source = bitmapImage,
                16 => OutputImage16.Source = bitmapImage
            };
        }

        await Task.CompletedTask;
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveBTN.IsEnabled = false;
            await GenerateIcons(ImagePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            SaveBTN.IsEnabled = true;
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            e.Handled = true;
            DragOperationDeferral def = e.GetDeferral();
            RandomAccessStreamReference s = await e.DataView.GetBitmapAsync();
            ImagePath = await e.DataView.GetTextAsync();

            BitmapImage bitmapImage = new();
            await bitmapImage.SetSourceAsync(await s.OpenReadAsync());
            MainImage.Source = bitmapImage;
            e.AcceptedOperation = DataPackageOperation.Copy;
            await SourceImageUpdated();
            def.Complete();
        }
        else if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            e.Handled = true;
            DragOperationDeferral def = e.GetDeferral();
            Uri s = await e.DataView.GetUriAsync();
            ImagePath = s.AbsolutePath;

            BitmapImage bmp = new(s);
            MainImage.Source = bmp;

            e.AcceptedOperation = DataPackageOperation.Copy;
            await SourceImageUpdated();
            def.Complete();
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.Handled = true;

            DragOperationDeferral def = e.GetDeferral();
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            foreach (IStorageItem item in storageItems)
            {
                if (item is StorageFile file)
                {
                    using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                    BitmapImage bitmapImage = new();
                    // Decode pixel sizes are optional
                    // It's generally a good optimisation to decode to match the size you'll display
                    // bitmapImage.DecodePixelHeight = decodePixelHeight;
                    // bitmapImage.DecodePixelWidth = decodePixelWidth;

                    await bitmapImage.SetSourceAsync(fileStream);
                    MainImage.Source = bitmapImage;
                    ImagePath = file.Path;
                    break;
                }
            }
            e.AcceptedOperation = DataPackageOperation.Copy;
            await SourceImageUpdated();
            def.Complete();
        }
    }

    private void Border_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }
}
