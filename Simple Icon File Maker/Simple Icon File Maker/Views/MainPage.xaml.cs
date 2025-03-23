using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.Constants;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Controls;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;
using Simple_Icon_File_Maker.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Simple_Icon_File_Maker;

public sealed partial class MainPage : Page
{
    ObservableCollection<IconSize> IconSizes { get; set; } = [];

    private readonly DispatcherTimer dispatcherTimer = new();
    private string ImagePath = "";
    private string OutPutPath = "";
    private readonly UndoRedo undoRedo = new();

    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ConfigBlankState();
        DataContext = ViewModel;
        dispatcherTimer.Interval = TimeSpan.FromMilliseconds(200);
        dispatcherTimer.Tick += DispatcherTimer_Tick;
        ViewModel = App.GetService<MainViewModel>();

        undoRedo.UndoButton = UndoButton;
        undoRedo.RedoButton = RedoButton;
    }

    private async void DispatcherTimer_Tick(object? sender, object e)
    {
        dispatcherTimer.Stop();

        await LoadFromImagePath();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        SupportedFilesTextBlock.Text = $"({string.Join(", ", FileTypes.SupportedImageFormats)})";

        if (App.cliArgs?.Length > 1)
            ImagePath = App.cliArgs[1];

        if (!App.GetService<IStoreService>().OwnsPro)
        {
            UpgradeProHypBtn.Visibility = Visibility.Visible;
            UpgradeProHypBtn2.Visibility = Visibility.Visible;
        }
        dispatcherTimer.Start();
    }

    private void LoadIconSizes()
    {
        IconSizes.Clear();
        List<IconSize> loadedSizes = App.GetService<IIconSizesService>().IconSizes;

        foreach (IconSize size in loadedSizes)
            if (!size.IsHidden)
                IconSizes.Add(size);

        CheckIfRefreshIsNeeded();
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

        // TODO order the icon frames by size and choose the largest size to compare
        MagickImage image = new(ImagePath);
        int smallerSide = (int)Math.Min(image.Width, image.Height);

        foreach (IconSize size in IconSizes)
            size.IsEnabled = size.SideLength <= smallerSide;

        SizeDisabledWarning.IsOpen = IconSizes.Any(x => !x.IsEnabled);

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

    private void ConfigBlankState() =>
        VisualStateManager.GoToState(this, UiStates.BlankState.ToString(), true);

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

            if (!ImagePath.IsSupportedImageFormat())
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

            if (!ImagePath.IsSupportedImageFormat())
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
        List<string> failedItemNames = [];
        // Iterate through all the items to find an image, stop at first success
        foreach (IStorageItem item in storageItems)
        {
            if (item is StorageFile file && file.IsSupportedImageFormat())
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
        LoadIconSizes();

        try
        {
            MagickImage image = new(ImagePath);
            MainImage.Source = image.ToImageSource();
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

        foreach (string extension in FileTypes.SupportedImageFormats)
            picker.FileTypeFilter.Add(extension);

        InitializeWithWindow.Initialize(picker, App.MainWindow.WindowHandle);

        StorageFile file = await picker.PickSingleFileAsync();

        if (openButton is not null)
            openButton.IsEnabled = true;

        if (file is null)
            return;

        ConfigUiThinking();
        ImagePath = file.Path;

        ViewModel.ImagePath = ImagePath;

        await LoadFromImagePath();
    }

    private void SelectIconSizes()
    {
        List<IconSize> chosenSizes = [];
        SelectTheseIcons([]);

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

        List<IconSize> orderedIcons = [.. IconSizes.OrderByDescending(size => size.SideLength)];
        IconSizes.Clear();

        foreach (IconSize size in orderedIcons)
            IconSizes.Add(size);

        int smallerSide = 0;

        if (Path.GetExtension(ImagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
            smallerSide = IconSizes.First(x => x.IsSelected).SideLength;

        foreach (IconSize size in IconSizes)
            size.IsEnabled = size.SideLength <= smallerSide;

        SizeDisabledWarning.IsOpen = IconSizes.Any(x => !x.IsEnabled);
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

        await RefreshPreviews();
    }

    private async Task RefreshPreviews()
    {
        UIElementCollection uIElements = PreviewsGrid.Children;

        Progress<int> progress = new(percent =>
        {
            InitialLoadProgressBar.Value = percent;
        });

        foreach (UIElement element in uIElements)
        {
            if (element is PreviewStack stack)
                await stack.GeneratePreviewImagesAsync(progress, ImagePath);
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
        savePicker.FileTypeChoices.Add("ICO File", [".ico"]);
        savePicker.DefaultFileExtension = ".ico";
        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath);

        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

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
                if (element is PreviewStack stack)
                    await stack.SaveAllImagesAsync(OutPutPath);
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
        savePicker.FileTypeChoices.Add("ICO File", [".ico"]);
        savePicker.DefaultFileExtension = ".ico";
        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath);

        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

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
        bool ownsPro = App.GetService<IStoreService>().OwnsPro;

        if (ownsPro)
        {
            EditSizesDialog editSizesDialog = new() { XamlRoot = Content.XamlRoot };
            editSizesDialog.Closed += (s, e) => { LoadIconSizes(); };
            _ = await editSizesDialog.ShowAsync();
        }
        else
        {
            BuyProDialog buyProDialog = new() { XamlRoot = Content.XamlRoot };
            _ = await buyProDialog.ShowAsync();
        }
    }

    private async void UpgradeToProHyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        BuyProDialog buyProDialog = new() { XamlRoot = Content.XamlRoot };
        _ = await buyProDialog.ShowAsync();
    }

    private async void BlackWhiteButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(ImagePath);
        string extension = Path.GetExtension(ImagePath);
        string bwFilePath = Path.Combine(sf.Path, $"{fileName}_bw{extension}");
        MagickImage image = new(ImagePath);

        image.Grayscale();
        image.AutoThreshold(AutoThresholdMethod.OTSU);
        await image.WriteAsync(bwFilePath);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, ImagePath, bwFilePath);
        undoRedo.AddUndo(undoRedoItem);

        ImagePath = bwFilePath;

        MainImage.Source = image.ToImageSource();

        await RefreshPreviews();
    }

    private async void BlackWhiteButton_Click2(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(ImagePath);
        string extension = Path.GetExtension(ImagePath);
        string bwkFilePath = Path.Combine(sf.Path, $"{fileName}_bwk{extension}");
        MagickImage image = new(ImagePath);

        image.Grayscale();
        image.AutoThreshold(AutoThresholdMethod.Kapur);
        await image.WriteAsync(bwkFilePath);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, ImagePath, bwkFilePath);
        undoRedo.AddUndo(undoRedoItem);

        ImagePath = bwkFilePath;

        MainImage.Source = image.ToImageSource();

        await RefreshPreviews();
    }

    private async void InvertButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(ImagePath);
        string extension = Path.GetExtension(ImagePath);
        string invFilePath = Path.Combine(sf.Path, $"{fileName}_inv{extension}");
        MagickImage image = new(ImagePath);

        image.Negate(Channels.RGB);
        await image.WriteAsync(invFilePath);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, ImagePath, invFilePath);
        undoRedo.AddUndo(undoRedoItem);

        ImagePath = invFilePath;

        MainImage.Source = image.ToImageSource();

        await RefreshPreviews();
    }

    private async void GrayScaleButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        string fileName = Path.GetFileNameWithoutExtension(ImagePath);
        string extension = Path.GetExtension(ImagePath);
        string grayFilePath = Path.Combine(sf.Path, $"{fileName}_gray{extension}");
        MagickImage image = new(ImagePath);

        image.Grayscale();
        await image.WriteAsync(grayFilePath);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage, ImagePath, grayFilePath);
        undoRedo.AddUndo(undoRedoItem);

        ImagePath = grayFilePath;

        MainImage.Source = image.ToImageSource();

        await RefreshPreviews();
    }

    private async void EditImageColorDropDownButton_Click(object sender, RoutedEventArgs e)
    {
        bool ownsPro = App.GetService<IStoreService>().OwnsPro;

        if (ownsPro)
            return;

        BuyProDialog buyProDialog = new() { XamlRoot = Content.XamlRoot };
        _ = await buyProDialog.ShowAsync();
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        bool ownsPro = App.GetService<IStoreService>().OwnsPro;

        if (ownsPro)
            return;

        if (sender is MenuFlyout flyout)
            flyout.Hide();
    }

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (!undoRedo.CanUndo)
            return;

        ImagePath = undoRedo.Undo();
        await RefreshPreviews();
    }

    private async void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (!undoRedo.CanRedo)
            return;

        ImagePath = undoRedo.Redo();
        await RefreshPreviews();
    }
}
