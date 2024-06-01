using ImageMagick;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.Controls;
using Simple_Icon_File_Maker.Models;
using Simple_Icon_File_Maker.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Store;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Simple_Icon_File_Maker;

public sealed partial class MainPage : Page
{
    ObservableCollection<IconSize> IconSizes { get; set; } = new(IconSize.GetAllSizes());

    readonly HashSet<string> SupportedImageFormats = new() { ".png", ".bmp", ".jpeg", ".jpg", ".ico" };

    private string ImagePath = "";
    private string OutPutPath = "";

    public MainPage()
    {
        InitializeComponent();

#if DEBUG
        licenseInformation = CurrentAppSimulator.LicenseInformation;
#else
        licenseInformation = CurrentApp.LicenseInformation;
#endif
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

    private void CheckBox_Tapped(object sender, TappedRoutedEventArgs e)
    {
        CheckIfRefreshIsNeeded();
    }

    private void CheckIfRefreshIsNeeded()
    {
        // iterate through all preview stacks and see if any of them need CanRefresh
        UIElementCollection allElements = PreviewsGrid.Children;

        bool anyRefreshAvailable = false;
        foreach (UIElement element in allElements)
        {
            if (element is PreviewStack stack)
            {
                _ = stack.ChooseTheseSizes(IconSizes);

                if (stack.CanRefresh)
                {
                    anyRefreshAvailable = true;
                    break;
                }
            }
        }

        if (anyRefreshAvailable)
            RefreshButton.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        else
            RefreshButton.Style = (Style)Application.Current.Resources["DefaultButtonStyle"];
    }

    private async void ClearAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        MainImage.Source = null;
        ImagePath = "-";

        foreach (UIElement? item in PreviewsGrid.Children)
            if (item is PreviewStack stack)
                stack.ClearChildren();

        ConfigUiWelcome();
        PreviewsGrid.Children.Clear();

        // Clear out all of the files in the cache folder
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        IReadOnlyList<StorageFile> cacheFiles = await sf.GetFilesAsync();
        foreach (StorageFile? file in cacheFiles)
            await file?.DeleteAsync();
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

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        ConfigUiThinking();
        errorInfoBar.IsOpen = false;
        ImagePath = string.Empty;
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;
        def.Complete();

        if (e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine("dropped bitmap");

            ImagePath = await e.DataView.GetTextAsync();

            if (!SupportedImageFormats.Contains(Path.GetExtension(ImagePath)))
            {
                Debug.WriteLine("bitmap, update not success");
                ConfigUiWelcome();
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

        InitialLoadProgressBar.Value = 0;

        try
        {
            MagickImage image = new(ImagePath);
            MainImage.Source = await image.ToImageSource();
        }
        catch (Exception ex)
        {
            errorInfoBar.IsOpen = true;
            errorInfoBar.Message = ex.Message;
            closeInfoBarStoryboard.Begin();
            ConfigUiWelcome();
        }

        List<IconSize> selectedSizes = IconSizes.Where(x => x.IsSelected).ToList();
        PreviewStack previewStack = new(ImagePath, selectedSizes);
        PreviewsGrid.Children.Add(previewStack);

        Progress<int> progress = new(percent =>
        {
            InitialLoadProgressBar.Value = percent;
        });

        bool generatedImages = await previewStack.InitializeAsync(progress);

        if (Path.GetExtension(ImagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
        {
            SelectIconSizes();
        }

        if (generatedImages)
        {
            ConfigUiShow();
            SaveBTN.IsEnabled = true;
            SaveAllBTN.IsEnabled = true;
        }
        else
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;
            ConfigUiWelcome();
        }
    }

    private async void InfoAppBarButton_Click(object sender, RoutedEventArgs e)
    {
        AboutDialog aboutWindow = new()
        {
            XamlRoot = Content.XamlRoot
        };

        _ = await aboutWindow.ShowAsync();
    }

    private async void OpenBTN_Click(object sender, RoutedEventArgs e)
    {
        Button? openButton = sender as Button;
        if (openButton is not null)
            openButton.IsEnabled = false;

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

        if (openButton is not null)
            openButton.IsEnabled = true;

        if (file is null)
            return;

        ConfigUiThinking();
        ImagePath = file.Path;

        await LoadFromImagePath();
    }

    private void SelectIconSizes()
    {
        List<IconSize> chosenSizes = new();
        SelectTheseIcons(Array.Empty<IconSize>());

        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
            if (element is PreviewStack stack)
                chosenSizes.AddRange(stack.ChosenSizes);

        chosenSizes = chosenSizes.Distinct().ToList();

        foreach (IconSize size in chosenSizes)
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

        List<IconSize> orderedIcons = IconSizes.OrderByDescending(size => size.SideLength).ToList();
        IconSizes.Clear();

        foreach (IconSize size in orderedIcons)
            IconSizes.Add(size);
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

        UIElementCollection uIElements = PreviewsGrid.Children;

        Progress<int> progress = new(percent =>
        {
            InitialLoadProgressBar.Value = percent;
        });

        foreach (UIElement element in uIElements)
        {
            if (element is PreviewStack stack)
                await stack.GeneratePreviewImagesAsync(progress);
        }

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

        OutPutPath = file.Path;

        try
        {
            SaveBTN.IsEnabled = false;
            SaveAllBTN.IsEnabled = false;

            UIElementCollection uIElements = PreviewsGrid.Children;

            foreach (UIElement element in uIElements)
            {
                if (element is PreviewStack stack)
                {
                    await stack.SaveAllImagesAsync(OutPutPath);
                }
            }
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

            UIElementCollection uIElements = PreviewsGrid.Children;

            foreach (UIElement element in uIElements)
                if (element is PreviewStack stack)
                    await stack.SaveIconAsync(OutPutPath);
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

    private void ZoomPreviewToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (ZoomPreviewToggleButton.IsChecked is not bool isZoomingPreview)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
        {
            if (element is PreviewStack stack)
            {
                stack.IsZoomingPreview = isZoomingPreview;
                stack.UpdateSizeAndZoom();
            }
        }
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
        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
        {
            if (element is PreviewStack stack)
            {
                stack.UpdateSizeAndZoom();
            }
        }
    }

    private async void EditSizes_Click(object sender, RoutedEventArgs e)
    {
        await BuyPro();
        return;

        bool ownsPro = await StoreService.OwnsPro();
        if (!ownsPro)
        {
            EditSizesDialog aboutWindow = new()
            {
                XamlRoot = Content.XamlRoot
            };

            _ = await aboutWindow.ShowAsync();
        }
        else
        {
            BuyProDialog buyProDialog = new()
            {
                XamlRoot = Content.XamlRoot
            };
            _ = await buyProDialog.ShowAsync();
        }

    }

    public LicenseInformation licenseInformation { get; private set; }
    private readonly string proName = "pro-features";

    private async Task BuyPro()
    {
        if (licenseInformation.ProductLicenses[proName].IsActive == false)
        {
            try
            {
                // The customer doesn't own this feature, so
                // show the purchase dialog.
                StorePurchaseProperties proProps = new(proName);
                StoreContext store = StoreContext.GetDefault();
                var result = await store.GetAssociatedStoreProductsAsync(new string[] { "Durable", "Consumable" });
                if (result.ExtendedError is not null)
                {
                    throw new Exception("Failed to get items from store");
                }

                foreach (var item in result.Products)
                {
                    StoreProduct product = item.Value;

                    if (product.InAppOfferToken == proName)
                    {
                        Window buyWindow = new();
                        IntPtr windowHandleSave = WindowNative.GetWindowHandle(buyWindow);
                        InitializeWithWindow.Initialize(product, windowHandleSave);

                        await product.RequestPurchaseAsync();
                    }
                }

                //Check the license state to determine if the in-app purchase was successful.
                if (licenseInformation.ProductLicenses[proName].IsActive)
                    return;
            }
            catch (Exception ex)
            {
                // The in-app purchase was not completed because
                // an error occurred.
                // FailedProPurchase.IsOpen = true;
                Debug.WriteLine(ex.Message);
#if DEBUG
                throw ex;
#endif
            }
        }
        else
        {
            // The customer already owns this feature.
        }
    }
}
