using ImageMagick;
using ImageMagick.ImageOptimizers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
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

namespace Simple_Icon_File_Maker
{
    public sealed partial class MainPage : Page
    {
        ObservableCollection<IconSize> IconSizes = new(IconSize.GetFullWindowsSizes());
        private string ImagePath = "";
        private string OutPutPath = "";
        private Size? SourceImageSize;
        public MainPage()
        {
            InitializeComponent();
        }

        List<IconSize> LastRefreshSizes { get; set; } = new();
        private void Border_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }

        private void CheckBox_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            CheckIfRefreshIsNeeded();
        }

        private void CheckIfRefreshIsNeeded()
        {
            List<IconSize> currentSizes = new(IconSizes.ToList());
            bool isCurrentUnChanged = true;

            for (int i = 0; i < currentSizes.Count; i++)
            {
                if (currentSizes[i] != LastRefreshSizes[i])
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

        private void CoinfigUiThinking() =>
            VisualStateManager.GoToState(this, "ThinkingState", true);

        private void ConfigUiShow() =>
            VisualStateManager.GoToState(this, "ImageSelectedState", true);

        private void ConfigUiWelcome() =>
            VisualStateManager.GoToState(this, "WelcomeState", true);

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

            using IMagickImage<ushort> firstPassimage = await imgFactory.CreateAsync(ImagePath);
            IMagickGeometry size = geoFactory.Create(
                Math.Max(SourceImageSize.Value.Width, SourceImageSize.Value.Height));
            size.IgnoreAspectRatio = false;
            size.FillArea = true;

            firstPassimage.Extent(size, Gravity.Center, MagickColor.FromRgba(0, 0, 0, 0));

            await firstPassimage.WriteAsync(croppedImagePath);

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

                image.Scale(iconSize);
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

            ImagesProcessingProgressRing.IsActive = false;
            ImagesProcessingProgressRing.Visibility = Visibility.Collapsed;
            return true;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            CoinfigUiThinking();
            SourceImageSize = null;
            if (e.DataView.Contains(StandardDataFormats.Bitmap))
            {
                Debug.WriteLine("bitmap");
                e.Handled = true;
                DragOperationDeferral def = e.GetDeferral();
                RandomAccessStreamReference s = await e.DataView.GetBitmapAsync();
                ImagePath = await e.DataView.GetTextAsync();
                def.Complete();
                e.AcceptedOperation = DataPackageOperation.Copy;

                bool success = await UpdateSourceImageFromStream(await s.OpenReadAsync());
                if (!success)
                {
                    Debug.WriteLine("bitmap, update not success");
                    e.AcceptedOperation = DataPackageOperation.None;
                    return;
                }
            }
            else if (e.DataView.Contains(StandardDataFormats.Uri))
            {
                e.Handled = true;
                DragOperationDeferral def = e.GetDeferral();
                Uri s = await e.DataView.GetUriAsync();
                ImagePath = s.AbsolutePath;
                Debug.WriteLine("URI");

                BitmapImage bmp = new(s);
                SourceImageSize = new(bmp.PixelWidth, bmp.PixelHeight);
                MainImage.Source = bmp;
                def.Complete();

                e.AcceptedOperation = DataPackageOperation.Copy;
                await SourceImageUpdated(Path.GetFileName(ImagePath));
            }
            else if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.Handled = true;
                Debug.WriteLine("StorageItem");

                DragOperationDeferral def = e.GetDeferral();
                IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

                // Itterate through all the items to find an image, stop at first success
                foreach (IStorageItem item in storageItems)
                {
                    if (item is StorageFile file &&
                        (file.FileType.ToLower() == ".png"
                        || file.FileType.ToLower() == ".bmp"
                        || file.FileType.ToLower() == ".jpeg"
                        || file.FileType.ToLower() == ".jpg"))
                    {
                        ImagePath = file.Path;
                        using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
                        def.Complete();
                        bool success = await UpdateSourceImageFromStream(fileStream);
                        if (!success)
                        {
                            Debug.WriteLine("StorageItem, no success");
                            e.AcceptedOperation = DataPackageOperation.None;
                            return;
                        }
                        // Found an image, stop looking
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        break;
                    }
                }
            }
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

            CoinfigUiThinking();
            ImagePath = file.Path;

            // Ensure the stream is disposed once the image is loaded
            using IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read);
            bool success = await UpdateSourceImageFromStream(fileStream);
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
            IntPtr hwndSave = WindowNative.GetWindowHandle(saveWindow);
            InitializeWithWindow.Initialize(savePicker, hwndSave);

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
            {
                if (child is not Viewbox vb)
                    continue;

                if (isZoomingPreview)
                    vb.Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill;
                else
                    vb.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;
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

                Image image = new()
                {
                    Source = bitmapImage,
                    Width = sideLength,
                    Height = sideLength,
                    Margin = new Thickness(5),
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill
                };
                Viewbox vb = new()
                {
                    Child = image,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.None
                };
                ToolTipService.SetToolTip(image, $"{sideLength} x {sideLength}");
                PreviewStackPanel.Children.Add(vb);
            }
            SetPreviewsZoom();
            await Task.CompletedTask;
        }

        private async Task<bool> UpdateSourceImageFromStream(IRandomAccessStream fileStream)
        {
            BitmapImage bitmapImage = new();
            // Decode pixel sizes are optional
            // It's generally a good optimisation to decode to match the size you'll display
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
    }
}
