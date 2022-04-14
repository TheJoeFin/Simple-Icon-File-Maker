using ImageMagick;
using ImageMagick.ImageOptimizers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
    private Size? SourceImageSize;
    private readonly AppWindow? m_AppWindow;

    public MainWindow()
    {
        InitializeComponent();

        m_AppWindow = GetAppWindowForCurrentWindow();
        m_AppWindow.SetIcon("SimpleIconMaker.ico");
        m_AppWindow.Title = "Simple Icon File Maker";
    }
    private AppWindow GetAppWindowForCurrentWindow()
    {
        IntPtr hWnd = WindowNative.GetWindowHandle(this);
        WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
        return AppWindow.GetFromWindowId(wndId);
    }

    private async void OpenBTN_Click(object sender, RoutedEventArgs e)
    {
        SourceImageSize = null;
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
        SourceImageSize = new(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
        MainImage.Source = bitmapImage;
        await SourceImageUpdated(Path.GetFileName(ImagePath));
    }

    private async Task SourceImageUpdated(string fileName)
    {
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string pathAndName = Path.Combine(sf.Path, fileName);
        bool success = await GenerateIcons(pathAndName, true);

        SaveBTN.IsEnabled = success;
        SaveAllBTN.IsEnabled = success;

        dropHere.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
    }

    private async Task<bool> GenerateIcons(string path, bool updatePreviews = false, bool saveAllFiles = false)
    {
        string? openedPath = Path.GetDirectoryName(path);
        string? name = Path.GetFileNameWithoutExtension(path);

        if (openedPath is null || name is null)
            return false;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string iconRootString = sf.Path;
        string croppedImagePath = Path.Combine(iconRootString, $"{name}Cropped.png");
        string iconOutputString = Path.Combine(openedPath, $"{name}.ico");
        if (Directory.Exists(iconRootString) == false)
            Directory.CreateDirectory(iconRootString);

        MagickImageFactory imgFactory = new();
        MagickGeometryFactory geoFactory = new();

        if (SourceImageSize is null)
            SourceImageSize = new Size((int)MainImage.RenderSize.Width, (int)MainImage.RenderSize.Height);

        if (string.IsNullOrWhiteSpace(ImagePath) == true)
        {
            ClearOutputImages();
            return false;
        }

        try
        {
            _ = await imgFactory.CreateAsync(ImagePath);
        }
        catch (Exception)
        {
            ClearOutputImages();
            return false;
        }

        using IMagickImage<ushort> firstPassimage = await imgFactory.CreateAsync(ImagePath);
        IMagickGeometry size = geoFactory.Create(
            Math.Min(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
        size.IgnoreAspectRatio = false;

        firstPassimage.Crop(size);
        await firstPassimage.WriteAsync(croppedImagePath);

        List<int> intList = new() { 256, 128, 64, 32, 16 };

        MagickImageCollection collection = new();
        Dictionary<int, string> imagePaths = new();

        foreach (int sideLength in intList)
        {
            using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);
            if (MainImage.ActualWidth < sideLength || MainImage.ActualHeight < sideLength)
                continue;

            IMagickGeometry iconSize = geoFactory.Create(sideLength, sideLength);
            iconSize.IgnoreAspectRatio = false;

            image.Resize(iconSize);
            image.Sharpen();

            string iconPath = $"{iconRootString}\\Image{sideLength}.png";
            string outputImagePath = $"{openedPath}\\{name}{sideLength}.png";
            await image.WriteAsync(iconPath);

            if (saveAllFiles == true)
                await image.WriteAsync(outputImagePath);

            collection.Add(iconPath);
            imagePaths.Add(sideLength, iconPath);
        }

        if (updatePreviews == true)
            await UpdatePreviewsAsync(imagePaths);

        await collection.WriteAsync(iconOutputString);

        IcoOptimizer icoOpti = new()
        {
            OptimalCompression = true
        };
        icoOpti.Compress(iconOutputString);

        return true;
    }

    private void ClearOutputImages()
    {
        OutputImage256.Source = null;
        OutputImage128.Source = null;
        OutputImage64.Source = null;
        OutputImage32.Source = null;
        OutputImage16.Source = null;
    }

    private async Task UpdatePreviewsAsync(Dictionary<int, string> imagePaths)
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
                16 => OutputImage16.Source = bitmapImage,
                _ => throw new Exception("Icon side length did not match output image size")
            };
        }

        await Task.CompletedTask;
    }

    private async void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        savePicker.FileTypeChoices.Add("ICO File", new List<string>() { ".ico" });
        savePicker.DefaultFileExtension = ".ico";
        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath);

        Window saveWindow = new();
        IntPtr hwndSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, hwndSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        string savePath = Path.Combine(file.Path);

        try
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
            await GenerateIcons(savePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            SaveBTN.IsEnabled = true;
            SaveAllBTN.IsEnabled = true;
        }
    }

    private async void SaveAllBTN_Click(object sender, RoutedEventArgs e)
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        savePicker.FileTypeChoices.Add("ICO File", new List<string>() { ".ico" });
        savePicker.DefaultFileExtension = ".ico";
        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath);

        Window saveWindow = new();
        IntPtr hwndSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, hwndSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        string savePath = Path.Combine(file.Path);

        try
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
            await GenerateIcons(savePath, false, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            SaveBTN.IsEnabled = true;
            SaveAllBTN.IsEnabled = true;
        }
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        SourceImageSize = null;
        if (e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            e.Handled = true;
            DragOperationDeferral def = e.GetDeferral();
            RandomAccessStreamReference s = await e.DataView.GetBitmapAsync();
            ImagePath = await e.DataView.GetTextAsync();

            BitmapImage bitmapImage = new();
            await bitmapImage.SetSourceAsync(await s.OpenReadAsync());
            SourceImageSize = new(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
            MainImage.Source = bitmapImage;
            e.AcceptedOperation = DataPackageOperation.Copy;
            await SourceImageUpdated(Path.GetFileName(ImagePath));
            def.Complete();
        }
        else if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            e.Handled = true;
            DragOperationDeferral def = e.GetDeferral();
            Uri s = await e.DataView.GetUriAsync();
            ImagePath = s.AbsolutePath;

            BitmapImage bmp = new(s);
            SourceImageSize = new(bmp.PixelWidth, bmp.PixelHeight);
            MainImage.Source = bmp;

            e.AcceptedOperation = DataPackageOperation.Copy;
            await SourceImageUpdated(Path.GetFileName(ImagePath));
            def.Complete();
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.Handled = true;

            DragOperationDeferral def = e.GetDeferral();
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            foreach (IStorageItem item in storageItems)
            {
                if (item is StorageFile file &&
                    (file.FileType == ".png"
                    || file.FileType == ".bmp"
                    || file.FileType == ".jpeg"
                    || file.FileType == ".jpg"))
                {
                    using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                    BitmapImage bitmapImage = new();
                    // Decode pixel sizes are optional
                    // It's generally a good optimisation to decode to match the size you'll display
                    // bitmapImage.DecodePixelHeight = decodePixelHeight;
                    // bitmapImage.DecodePixelWidth = decodePixelWidth;

                    await bitmapImage.SetSourceAsync(fileStream);
                    SourceImageSize = new(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                    MainImage.Source = bitmapImage;
                    ImagePath = file.Path;
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    await SourceImageUpdated(Path.GetFileName(ImagePath));
                    break;
                }
            }
            def.Complete();
        }
    }

    private void Border_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
    }

    private void InfoAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        AboutWindow aboutWindow = new();
        aboutWindow.Activate();
    }

    private async void ClearAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        MainImage.Source = null;
        ImagePath = "-";
        await SourceImageUpdated("");
    }
}
