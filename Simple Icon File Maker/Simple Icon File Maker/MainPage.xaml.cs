using ImageMagick;
using ImageMagick.ImageOptimizers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Simple_Icon_File_Maker.Controls;
using Simple_Icon_File_Maker.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using WinRT.Interop;

namespace Simple_Icon_File_Maker;

public sealed partial class MainPage : Page
{
    ObservableCollection<IconSize> IconSizes { get; set; } = new(IconSize.GetAllSizes());

    List<IconSize> LastRefreshSizes { get; set; } = new();
    readonly HashSet<string> SupportedImageFormats = new() { ".png", ".bmp", ".jpeg", ".jpg", ".ico" };

    private string ImagePath = "";
    private string OutPutPath = "";
    private Size? SourceImageSize;
    public MainPage()
    {
        InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        SelectTheseIcons(IconSize.GetWindowsSizesFull());

        SupportedFilesTextBlock.Text = $"({string.Join(", ", SupportedImageFormats)})";
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }

    private void Border_DragOver(object sender, DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine($"contains Bitmap");
            e.AcceptedOperation = DataPackageOperation.Copy;
            return;
        }
        else if (dataView.Contains(StandardDataFormats.Uri))
        {
            Debug.WriteLine($"contains Uri");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
        else if (dataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine($"contains StorageItems");
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    private void CheckBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        CheckIfRefreshIsNeeded();
    }

    private void CheckIfRefreshIsNeeded()
    {
        if (LastRefreshSizes.Count < 1)
            return;

        List<IconSize> currentSizes = new(IconSizes.Where(i => i.IsSelected).ToList());
        bool isCurrentUnChanged = true;

        for (int i = 0; i < currentSizes.Count; i++)
        {
            if (!currentSizes[i].Equals(LastRefreshSizes[i]))
            {
                isCurrentUnChanged = false;
                break;
            }
        }

        if (isCurrentUnChanged)
            RefreshButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        else
            RefreshButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
    }

    private async void ClearAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        MainImage.Source = null;
        ImagePath = "-";
        await SourceImageUpdated("");
        ConfigUiWelcome();
    }

    private void ClearOutputImages()
    {
        PreviewStackPanel.Children.Clear();

        ImagesProcessingProgressRing.IsActive = false;
        ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = false;

        IconSizesListView.UpdateLayout();
        CheckIfRefreshIsNeeded();
    }

    private void ConfigUiThinking() =>
    VisualStateManager.GoToState(this, UiStates.ThinkingState.ToString(), true);

    private void ConfigUiShow() =>
        VisualStateManager.GoToState(this, UiStates.ImageSelectedState.ToString(), true);

    private void ConfigUiWelcome() =>
        VisualStateManager.GoToState(this, UiStates.WelcomeState.ToString(), true);

    private async Task<bool> GenerateIcons(string path, bool updatePreviews = false, bool saveAllFiles = false)
    {
        LastRefreshSizes.Clear();
        ImagesProcessingProgressRing.Visibility = Visibility.Visible;
        ImagesProcessingProgressRing.IsActive = true;

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

        SourceImageSize ??= new Size((int)MainImage.RenderSize.Width, (int)MainImage.RenderSize.Height);

        int smallerSide = Math.Min(SourceImageSize.Value.Width, SourceImageSize.Value.Height);

        foreach (IconSize iconSize in IconSizes)
        {
            iconSize.IsEnabled = true;
            if (iconSize.SideLength > smallerSide)
                iconSize.IsEnabled = false;
        }

        foreach (IconSize iconSize in IconSizes)
            if (iconSize.IsSelected)
                LastRefreshSizes.Add(new(iconSize));

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

        using IMagickImage<ushort> firstPassImage = await imgFactory.CreateAsync(ImagePath);
        IMagickGeometry size = geoFactory.Create(
            Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
        size.IgnoreAspectRatio = false;
        size.FillArea = true;

        firstPassImage.Extent(size, Gravity.Center, MagickColor.FromRgba(0, 0, 0, 0));

        await firstPassImage.WriteAsync(croppedImagePath);

        MagickImageCollection collection = new();
        Dictionary<int, string> imagePaths = new();

        List<int> selectedSizes = IconSizes.Where(s => s.IsSelected == true).Select(s => s.SideLength).ToList();

        foreach (int sideLength in selectedSizes)
        {
            using IMagickImage<ushort> image = await imgFactory.CreateAsync(croppedImagePath);
            if (smallerSide < sideLength)
                continue;

            IMagickGeometry iconSize = geoFactory.Create(sideLength, sideLength);
            iconSize.IgnoreAspectRatio = false;

            if (smallerSide > sideLength)
            {
                image.Scale(iconSize);
                image.Sharpen();
            }

            string iconPath = $"{iconRootString}\\Image{sideLength}.png";
            string outputImagePath = $"{openedPath}\\{name}{sideLength}.png";
            await image.WriteAsync(iconPath, MagickFormat.Png32);

            if (saveAllFiles == true)
                await image.WriteAsync(outputImagePath, MagickFormat.Png32);

            collection.Add(iconPath);
            imagePaths.Add(sideLength, iconPath);
        }

        try
        {
            if (updatePreviews == true)
                await UpdatePreviewsAsync(imagePaths);
            else
            {
                await collection.WriteAsync(iconOutputString);

                IcoOptimizer icoOpti = new()
                {
                    OptimalCompression = true
                };
                icoOpti.Compress(iconOutputString);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Generating Icons Exception " + ex.Message);
            return false;
        }
        finally
        {
            ImagesProcessingProgressRing.IsActive = false;
            ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
        }
        return true;
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        ConfigUiThinking();
        errorInfoBar.IsOpen = false;
        SourceImageSize = null;
        ImagePath = string.Empty;
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;

        if (e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine("dropped bitmap");

            ImagePath = await e.DataView.GetTextAsync();

            if (!SupportedImageFormats.Contains(Path.GetExtension(ImagePath)))
            {
                Debug.WriteLine("bitmap, update not success");
                ConfigUiWelcome();
                def.Complete();
                return;
            }

            await LoadFromImagePath();
        }
        else if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            Debug.WriteLine("dropped URI");
            Uri s = await e.DataView.GetUriAsync();

            string extension = Path.GetExtension(s.AbsolutePath) ?? string.Empty;

            if (!SupportedImageFormats.Contains(extension))
            {
                Debug.WriteLine("dropped URI, not supported");
                ConfigUiWelcome();
                def.Complete();
                return;
            }

            ImagePath = s.AbsolutePath;
            await LoadFromImagePath();

        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine("Dropped StorageItem");
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            await TryToOpenStorageItems(storageItems);
        }

        def.Complete();
    }

    private async Task TryToOpenStorageItems(IReadOnlyList<IStorageItem> storageItems)
    {
        List<string> failedItemNames = new();
        // Iterate through all the items to find an image, stop at first success
        foreach (IStorageItem item in storageItems)
        {
            if (item is StorageFile file &&
                SupportedImageFormats.Contains(file.FileType.ToLowerInvariant()))
            {
                ImagePath = file.Path;
                await LoadFromImagePath();
                // Found an image, stop looking
                return;
            }
            else { failedItemNames.Add($"File type not supported: {item.Name}"); }
        }
        Debug.WriteLine("StorageItem, not supported");
        ShowErrorOnItem(string.Join($",{Environment.NewLine}", failedItemNames));
        ConfigUiWelcome();
    }

    private void ShowErrorOnItem(string errorMessage)
    {
        errorInfoBar.Message = errorMessage;
        errorInfoBar.IsOpen = true;
    }

    private async Task LoadFromImagePath()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            ConfigUiWelcome();
            return;
        }

        if (Path.GetExtension(ImagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
        {
            await LoadIconFile();
            return;
        }

        StorageFile imageFile = await StorageFile.GetFileFromPathAsync(ImagePath);
        using IRandomAccessStream fileStream = await imageFile.OpenAsync(FileAccessMode.Read);
        _ = await UpdateSourceImageFromStream(fileStream);
    }

    private async void InfoAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        AboutDialog aboutWindow = new()
        {
            XamlRoot = this.Content.XamlRoot
        };

        _ = await aboutWindow.ShowAsync();
    }

    private async void OpenBTN_Click(object sender, RoutedEventArgs e)
    {
        SourceImageSize = null;
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        foreach (string extension in SupportedImageFormats)
            picker.FileTypeFilter.Add(extension);

        Window window = new();
        IntPtr windowHandle = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(picker, windowHandle);

        StorageFile file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        ConfigUiThinking();
        ImagePath = file.Path;

        await LoadFromImagePath();
    }

    private async Task LoadIconFile()
    {
        bool success = false;
        MagickImageCollection collection = new(ImagePath);
        Dictionary<int, string> iconImages = new();
        List<IconSize> sizesOfIcons = new();
        StorageFolder? sf = ApplicationData.Current.LocalCacheFolder;
        int biggestSide = 0;
        string biggestPath = string.Empty;

        foreach (MagickImage image in collection.Cast<MagickImage>())
        {
            Debug.WriteLine($"Image: {image.Width}x{image.Height}");
            string imageName = $"{Path.GetFileNameWithoutExtension(ImagePath)}-{image.Width}.png";

            string imagePath = Path.Combine(sf.Path, imageName);
            await image.WriteAsync(imagePath, MagickFormat.Png32);

            iconImages.Add(image.Width, imagePath);
            IconSize iconSizeOfIconFrame = new(image.Width)
            {
                IsSelected = true,
            };
            sizesOfIcons.Add(iconSizeOfIconFrame);

            if (image.Width > biggestSide)
            {
                biggestSide = image.Width;
                biggestPath = imagePath;
            }
        }

        IconSize[] empty = Array.Empty<IconSize>();
        SelectTheseIcons(empty);

        foreach (IconSize size in sizesOfIcons)
        {
            bool isAlreadyInList = false;
            foreach (IconSize setSize in IconSizes)
            {

                if (setSize.SideLength == size.SideLength)
                {
                    isAlreadyInList = true;
                    setSize.IsSelected = true;
                    break;
                }
            }

            if (!isAlreadyInList)
                IconSizes.Add(size);
        }

        var orderedIcons = IconSizes.OrderByDescending(size => size.SideLength).ToList();
        IconSizes.Clear();

        foreach (IconSize size in orderedIcons)
            IconSizes.Add(size);

        if (!string.IsNullOrEmpty(biggestPath))
        {
            StorageFile imageSF = await StorageFile.GetFileFromPathAsync(biggestPath);
            using IRandomAccessStream fileStream = await imageSF.OpenAsync(FileAccessMode.Read);
            BitmapImage bitmapImage = new()
            {
                DecodePixelHeight = biggestSide,
                DecodePixelWidth = biggestSide
            };

            await bitmapImage.SetSourceAsync(fileStream);
            MainImage.Source = bitmapImage;
        }

        try
        {
            await UpdatePreviewsAsync(iconImages);
            success = true;
        }
        catch (Exception) { }

        if (success)
            ConfigUiShow();
        else
            ConfigUiWelcome();
    }

    private async void OpenFolderBTN_Click(object sender, RoutedEventArgs e)
    {
        string? outputDirectory = Path.GetDirectoryName(OutPutPath);
        if (outputDirectory is null)
            return;

        Uri uri = new(outputDirectory);
        LauncherOptions options = new()
        {
            TreatAsUntrusted = false,
            DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseLess
        };

        _ = await Launcher.LaunchUriAsync(uri, options);
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
        await SourceImageUpdated(Path.GetFileName(ImagePath));

        bool isAnySizeSelected = IconSizes.Any(x => x.IsSelected);
        if (!IconSizes.Any(x => x.IsSelected))
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
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
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        OutPutPath = Path.Combine(file.Path);

        try
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
            await GenerateIcons(OutPutPath, false, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            SaveBTN.IsEnabled = true;
            SaveAllBTN.IsEnabled = true;
            OpenFolderBTN.Visibility = Visibility.Visible;
        }
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
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(savePicker, windowHandleSave);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        OutPutPath = Path.Combine(file.Path);

        try
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
            await GenerateIcons(OutPutPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            SaveBTN.IsEnabled = true;
            SaveAllBTN.IsEnabled = true;
            OpenFolderBTN.Visibility = Visibility.Visible;
        }
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = true;

        IconSizesListView.UpdateLayout();
        CheckIfRefreshIsNeeded();
    }

    private void SetPreviewsZoom()
    {
        UIElementCollection previewBoxes = PreviewStackPanel.Children;

        if (ZoomPreviewToggleButton.IsChecked is not bool isZoomingPreview)
            return;

        foreach (var child in previewBoxes)
            if (child is PreviewImage img)
            {
                if (!double.IsNaN(PreviewCard.ActualWidth) && PreviewCard.ActualWidth > 40)
                    img.ZoomedWidthSpace = (int)PreviewCard.ActualWidth - 24;
                img.ZoomPreview = isZoomingPreview;
            }
    }

    private async Task SourceImageUpdated(string fileName)
    {
        PreviewStackPanel.Children.Clear();
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string pathAndName = Path.Combine(sf.Path, fileName);
        bool success = await GenerateIcons(pathAndName, true);

        SaveBTN.IsEnabled = success;
        SaveAllBTN.IsEnabled = success;

        if (success)
            ConfigUiShow();
        else
            ConfigUiWelcome();
    }
    private async Task UpdatePreviewsAsync(Dictionary<int, string> imagePaths)
    {
        string originalName = Path.GetFileNameWithoutExtension(ImagePath);
        foreach (var pair in imagePaths)
        {
            if (pair.Value is not string imagePath)
                return;

            int sideLength = pair.Key;

            StorageFile imageSF = await StorageFile.GetFileFromPathAsync(imagePath);
            
            PreviewImage image = new(imageSF, sideLength, originalName);

            PreviewStackPanel.Children.Add(image);
        }
        SetPreviewsZoom();
        await Task.CompletedTask;
    }

    private async Task<bool> UpdateSourceImageFromStream(IRandomAccessStream fileStream)
    {
        BitmapImage bitmapImage = new();
        // Decode pixel sizes are optional
        // It's generally a good optimization to decode to match the size you'll display
        // bitmapImage.DecodePixelHeight = decodePixelHeight;
        // bitmapImage.DecodePixelWidth = decodePixelWidth;

        try
        {
            await bitmapImage.SetSourceAsync(fileStream);
        }
        catch (Exception ex)
        {
            errorInfoBar.IsOpen = true;
            errorInfoBar.Message = ex.Message;
            closeInfoBarStoryboard.Begin();
            ConfigUiWelcome();
            return false;
        }
        SourceImageSize = new(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
        MainImage.Source = bitmapImage;
        await SourceImageUpdated(Path.GetFileName(ImagePath));
        return true;
    }
    private void ZoomPreviewToggleButton_Click(object sender, RoutedEventArgs e)
    {
        SetPreviewsZoom();
    }

    private void SelectWebButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTheseIcons(IconSize.GetIdealWebSizesFull());
        CheckIfRefreshIsNeeded();
    }

    private void SelectWindowsButton_Click(object sender, RoutedEventArgs e)
    {
        SelectTheseIcons(IconSize.GetWindowsSizesFull());
        CheckIfRefreshIsNeeded();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        SetPreviewsZoom();
    }
}

public enum UiStates
{
    WelcomeState = 0,
    ThinkingState = 1,
    ImageSelectedState = 2,
    UnsupportedFileFormat = 3,
}
