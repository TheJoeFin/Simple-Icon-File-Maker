using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Simple_Icon_File_Maker.Constants;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    private readonly System.Timers.Timer _countdownTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private const int CountdownDurationMs = 3000; // 3 seconds
    private const int CountdownIntervalMs = 50; // Update every 50ms for smooth progress
    private int _countdownElapsedMs = 0;
    private System.Timers.Timer _settingsSaveTimer = new();
    private readonly UndoRedo _undoRedo = new();

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IIconSizesService _iconSizesService;
    private readonly IStoreService _storeService;

    [ObservableProperty]
    public partial bool IsAutoRefreshEnabled { get; set; } = true;

    [ObservableProperty]
    public partial double CountdownProgress { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsCountdownActive { get; set; } = false;

    [ObservableProperty]
    public partial string ImagePath { get; set; } = "";

    [ObservableProperty]
    public partial string OutputPath { get; set; } = "";

    [ObservableProperty]
    public partial ImageSource? MainImageSource { get; set; }

    [ObservableProperty]
    public partial int LoadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsLoading { get; set; } = false;

    [ObservableProperty]
    public partial bool IsImageSelected { get; set; } = false;

    [ObservableProperty]
    public partial bool SizeDisabledWarningIsOpen { get; set; } = false;

    [ObservableProperty]
    public partial bool CanSave { get; set; } = false;

    [ObservableProperty]
    public partial bool OpenFolderButtonVisible { get; set; } = false;

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = "";

    [ObservableProperty]
    public partial bool ErrorInfoBarIsOpen { get; set; } = false;

    [ObservableProperty]
    public partial string SupportedFilesText { get; set; } = "";

    [ObservableProperty]
    public partial bool ShowUpgradeToProButton { get; set; } = false;

    [ObservableProperty]
    public partial bool RefreshButtonIsAccent { get; set; } = false;

    [ObservableProperty]
    public partial bool CanUndo { get; set; } = false;

    [ObservableProperty]
    public partial bool CanRedo { get; set; } = false;
    public ObservableCollection<IconSize> IconSizes { get; } = [];

    // Reference to UI elements that need to be accessed
    public Grid? PreviewsGrid { get; set; }
    public Image? MainImage { get; set; }
    public ProgressBar? InitialLoadProgressBar { get; set; }

    public event EventHandler? CountdownCompleted;

    private INavigationService NavigationService { get; }

    public MainViewModel(
            INavigationService navigationService,
            ILocalSettingsService localSettingsService,
            IIconSizesService iconSizesService,
            IStoreService storeService)
    {
        NavigationService = navigationService;
        _localSettingsService = localSettingsService;
        _iconSizesService = iconSizesService;
        _storeService = storeService;
        _dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        _countdownTimer = new System.Timers.Timer(CountdownIntervalMs);
        _countdownTimer.Elapsed += OnCountdownTick;
        _countdownTimer.AutoReset = true;

        _settingsSaveTimer.AutoReset = false;
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(300).TotalMilliseconds;
        _settingsSaveTimer.Elapsed += SettingsSaveTimer_Elapsed;

        SupportedFilesText = $"({string.Join(", ", FileTypes.SupportedImageFormats)})";
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async void SettingsSaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _settingsSaveTimer.Stop();
        await _localSettingsService.SaveSettingAsync<bool>(nameof(IsAutoRefreshEnabled), IsAutoRefreshEnabled);
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            IsAutoRefreshEnabled = await _localSettingsService.ReadSettingAsync<bool>(nameof(IsAutoRefreshEnabled));
        }
        catch (Exception)
        {
            IsAutoRefreshEnabled = true;
        }

        ShowUpgradeToProButton = !_storeService.OwnsPro;

        // Load CLI args if present
        if (App.cliArgs?.Length > 1)
        {
            ImagePath = App.cliArgs[1];
        }

        LoadIconSizes();

        // Delayed load to allow UI to initialize
        await Task.Delay(200);
        if (!string.IsNullOrWhiteSpace(ImagePath))
        {
            await LoadFromImagePathAsync();
        }
    }

    public void OnNavigatedFrom()
    {
        StopCountdown();
        _countdownTimer.Dispose();
    }

    [RelayCommand]
    public void NavigateToAbout()
    {
        NavigationService.NavigateTo(typeof(AboutViewModel).FullName!);
    }

    [RelayCommand]
    public async Task NavigateToMulti()
    {
        bool ownsPro = _storeService.OwnsPro;

        if (ownsPro)
        {
            FolderPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
                CommitButtonText = "Select",
                FileTypeFilter = { "*" }
            };

            InitializeWithWindow.Initialize(picker, App.MainWindow.WindowHandle);

            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder is not null)
                NavigationService.NavigateTo(typeof(MultiViewModel).FullName!, folder);
        }
        else
        {
            BuyProDialog buyProDialog = new();
            _ = await NavigationService.ShowModal(buyProDialog);
        }
    }

    [RelayCommand]
    public async Task BrowseAndSelectImage()
    {
        FileOpenPicker picker = new()
        {
            ViewMode = PickerViewMode.Thumbnail,
            SuggestedStartLocation = PickerLocationId.PicturesLibrary
        };

        foreach (string extension in FileTypes.SupportedImageFormats)
            picker.FileTypeFilter.Add(extension);

        InitializeWithWindow.Initialize(picker, App.MainWindow.WindowHandle);

        StorageFile file = await picker.PickSingleFileAsync();

        if (file is null)
            return;

        ImagePath = file.Path;
        await LoadFromImagePathAsync();
    }

    [RelayCommand]
    public async Task ClearImage()
    {
        MainImageSource = null;
        ImagePath = "-";

        if (PreviewsGrid != null)
        {
            foreach (UIElement? item in PreviewsGrid.Children)
            {
                if (item is Controls.PreviewStack stack)
                    stack.ClearChildren();
            }
            PreviewsGrid.Children.Clear();
        }

        IsImageSelected = false;
        CanSave = false;
        OpenFolderButtonVisible = false;

        // Clear out all of the files in the cache folder
        StorageFolder sf = ApplicationData.Current.LocalCacheFolder;
        IReadOnlyList<StorageFile> cacheFiles = await sf.GetFilesAsync();
        foreach (StorageFile? file in cacheFiles)
            await file?.DeleteAsync();
    }

    [RelayCommand]
    public void SelectAllSizes()
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = true;

        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void ClearSizeSelection()
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = false;

        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void SelectWindowsSizes()
    {
        SelectTheseIcons(IconSize.GetWindowsSizesFull());
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void SelectWebSizes()
    {
        SelectTheseIcons(IconSize.GetIdealWebSizesFull());
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void SizeCheckboxTapped()
    {
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public async Task RefreshPreviews()
    {
        RefreshButtonIsAccent = false;
        StopCountdown();

        if (PreviewsGrid == null)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        Progress<int> progress = new(percent =>
        {
            LoadProgress = percent;
        });

        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
                await stack.GeneratePreviewImagesAsync(progress, ImagePath);
        }

        bool isAnySizeSelected = IconSizes.Any(x => x.IsSelected);
        CanSave = isAnySizeSelected;
    }

    [RelayCommand]
    public void ZoomPreviews(bool isZooming)
    {
        if (PreviewsGrid == null)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                stack.IsZoomingPreview = isZooming;
                stack.UpdateSizeAndZoom();
            }
        }
    }

    [RelayCommand]
    public async Task SaveIcon()
    {
        FileSavePicker savePicker = CreateSavePicker();
        await FilePickerHelper.TrySetSuggestedFolderFromSourceImage(savePicker, ImagePath);
        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        OutputPath = file.Path;

        try
        {
            CanSave = false;

            if (PreviewsGrid != null)
            {
                foreach (UIElement element in PreviewsGrid.Children)
                {
                    if (element is Controls.PreviewStack stack)
                        await stack.SaveIconAsync(OutputPath);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            CanSave = true;
            OpenFolderButtonVisible = true;
        }
    }

    [RelayCommand]
    public async Task SaveAllImages()
    {
        FileSavePicker savePicker = CreateSavePicker();
        await FilePickerHelper.TrySetSuggestedFolderFromSourceImage(savePicker, ImagePath);
        InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

        StorageFile file = await savePicker.PickSaveFileAsync();

        if (file is null)
            return;

        OutputPath = file.Path;

        try
        {
            CanSave = false;

            if (PreviewsGrid != null)
            {
                foreach (UIElement element in PreviewsGrid.Children)
                {
                    if (element is Controls.PreviewStack stack)
                        await stack.SaveAllImagesAsync(OutputPath);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to Generate Icons: {ex.Message}");
        }
        finally
        {
            CanSave = true;
            OpenFolderButtonVisible = true;
        }
    }

    [RelayCommand]
    public async Task OpenOutputFolder()
    {
        string? outputDirectory = Path.GetDirectoryName(OutputPath);
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

    [RelayCommand]
    public async Task EditIconSizes()
    {
        bool ownsPro = _storeService.OwnsPro;

        if (ownsPro)
        {
            EditSizesDialog editSizesDialog = new();
            editSizesDialog.Closed += (s, e) => { LoadIconSizes(); };
            _ = await NavigationService.ShowModal(editSizesDialog);
        }
        else
        {
            BuyProDialog buyProDialog = new();
            _ = await NavigationService.ShowModal(buyProDialog);
        }
    }

    [RelayCommand]
    public async Task UpgradeToPro()
    {
        BuyProDialog buyProDialog = new();
        _ = await NavigationService.ShowModal(buyProDialog);
    }

    [RelayCommand]
    public async Task CheckForProEditColor()
    {
        bool ownsPro = _storeService.OwnsPro;

        if (ownsPro)
            return;

        BuyProDialog buyProDialog = new();
        _ = await NavigationService.ShowModal(buyProDialog);
    }

    [RelayCommand]
    public async Task ApplyGrayscale()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        string newPath = await ImageHelper.ApplyGrayscaleAsync(ImagePath, MainImage);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
        _undoRedo.AddUndo(undoRedoItem);
        UpdateUndoRedoState();

        ImagePath = newPath;
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task ApplyBlackWhiteOtsu()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        string newPath = await ImageHelper.ApplyBlackWhiteOtsuAsync(ImagePath, MainImage);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
        _undoRedo.AddUndo(undoRedoItem);
        UpdateUndoRedoState();

        ImagePath = newPath;
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task ApplyBlackWhiteKapur()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        string newPath = await ImageHelper.ApplyBlackWhiteKapurAsync(ImagePath, MainImage);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
        _undoRedo.AddUndo(undoRedoItem);
        UpdateUndoRedoState();

        ImagePath = newPath;
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task ApplyInvert()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        string newPath = await ImageHelper.ApplyInvertAsync(ImagePath, MainImage);

        MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
        _undoRedo.AddUndo(undoRedoItem);
        UpdateUndoRedoState();

        ImagePath = newPath;
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task Undo()
    {
        if (!_undoRedo.CanUndo)
            return;

        ImagePath = _undoRedo.Undo();
        UpdateUndoRedoState();
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task Redo()
    {
        if (!_undoRedo.CanRedo)
            return;

        ImagePath = _undoRedo.Redo();
        UpdateUndoRedoState();
        await RefreshPreviews();
    }

    [RelayCommand]
    public async Task PasteFromClipboard()
    {
        try
        {
            IsLoading = true;
            ErrorInfoBarIsOpen = false;

            string? clipboardImagePath = await ClipboardHelper.TryGetImageFromClipboardAsync();

            if (clipboardImagePath != null)
            {
                ImagePath = clipboardImagePath;
                await LoadFromImagePathAsync();
            }
            else
            {
                ShowError("No image found in clipboard. Copy an image and try again.");
                IsLoading = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error pasting from clipboard: {ex.Message}");
            ShowError($"Error pasting from clipboard: {ex.Message}");
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task HandleDrop(DragEventArgs e)
    {
        IsLoading = true;
        ErrorInfoBarIsOpen = false;
        ImagePath = string.Empty;

        if (e.DataView.Contains(StandardDataFormats.Bitmap))
        {
            Debug.WriteLine("dropped bitmap");
            ImagePath = await e.DataView.GetTextAsync();

            if (!ImagePath.IsSupportedImageFormat())
            {
                Debug.WriteLine("bitmap, update not success");
                IsLoading = false;
                return;
            }

            await LoadFromImagePathAsync();
        }
        else if (e.DataView.Contains(StandardDataFormats.Uri))
        {
            Debug.WriteLine("dropped URI");
            Uri s = await e.DataView.GetUriAsync();

            if (!s.AbsolutePath.IsSupportedImageFormat())
            {
                Debug.WriteLine("dropped URI, not supported");
                IsLoading = false;
                return;
            }

            ImagePath = s.AbsolutePath;
            await LoadFromImagePathAsync();
        }
        else if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            Debug.WriteLine("Dropped StorageItem");
            IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

            string? imagePath = await StorageItemHelper.TryGetImagePathFromStorageItems(storageItems);

            if (imagePath != null)
            {
                ImagePath = imagePath;
                await LoadFromImagePathAsync();
            }
            else
            {
                List<string> failedItems = StorageItemHelper.GetFailedItemNames(storageItems);
                ShowError(string.Join($",{Environment.NewLine}", failedItems));
                IsLoading = false;
            }
        }
    }

    public static void HandleDragOver(DragEventArgs e)
    {
        DataPackageView dataView = e.DataView;

        if (dataView.Contains(StandardDataFormats.Bitmap) ||
            dataView.Contains(StandardDataFormats.Uri) ||
            dataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
        }
    }

    public void UpdatePreviewsOnSizeChange()
    {
        if (PreviewsGrid == null)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                stack.UpdateSizeAndZoom();
            }
        }
    }

    public bool CheckIfProFeatureAllowed()
    {
        return _storeService.OwnsPro;
    }

    // Private helper methods

    private void LoadIconSizes()
    {
        IconSizes.Clear();
        List<IconSize> loadedSizes = _iconSizesService.IconSizes;

        foreach (IconSize size in loadedSizes)
        {
            if (!size.IsHidden)
                IconSizes.Add(size);
        }

        CheckIfRefreshIsNeeded();
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }

    private void CheckIfRefreshIsNeeded()
    {
        StopCountdown();

        if (PreviewsGrid == null)
            return;

        UIElementCollection allElements = PreviewsGrid.Children;

        bool anyRefreshAvailable = false;
        foreach (UIElement element in allElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                _ = stack.ChooseTheseSizes(IconSizes);

                if (stack.CanRefresh)
                {
                    anyRefreshAvailable = true;
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(ImagePath) && ImagePath != "-")
        {
            int smallerSide = ImageHelper.GetSmallerImageSide(ImagePath);

            foreach (IconSize size in IconSizes)
                size.IsEnabled = size.SideLength <= smallerSide;

            SizeDisabledWarningIsOpen = IconSizes.Any(x => !x.IsEnabled);
        }

        if (anyRefreshAvailable)
        {
            RefreshButtonIsAccent = true;
            StartCountdown();
        }
        else
        {
            RefreshButtonIsAccent = false;
        }
    }

    private async Task LoadFromImagePathAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            IsLoading = false;
            IsImageSelected = false;
            return;
        }

        IsLoading = true;
        OutputPath = "";
        LoadProgress = 0;
        LoadIconSizes();

        try
        {
            MagickImage? image = await ImageHelper.LoadImageAsync(ImagePath);

            if (image == null)
            {
                ShowError("Failed to load image");
                IsLoading = false;
                IsImageSelected = false;
                return;
            }

            MainImageSource = image.ToImageSource();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            IsLoading = false;
            IsImageSelected = false;
            return;
        }

        List<IconSize> selectedSizes = [.. IconSizes.Where(x => x.IsSelected)];
        Controls.PreviewStack previewStack = new(ImagePath, selectedSizes);

        PreviewsGrid?.Children.Add(previewStack);

        Progress<int> progress = new(percent =>
 {
     LoadProgress = percent;
 });

        bool generatedImages = await previewStack.InitializeAsync(progress);

        if (Path.GetExtension(ImagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
        {
            SelectIconSizesFromPreview();
        }

        if (generatedImages)
        {
            IsImageSelected = true;
            CanSave = true;
        }
        else
        {
            CanSave = false;
            IsImageSelected = false;
        }

        IsLoading = false;
    }

    private void SelectIconSizesFromPreview()
    {
        if (PreviewsGrid == null)
            return;

        List<IconSize> chosenSizes = [];
        SelectTheseIcons([]);

        UIElementCollection uIElements = PreviewsGrid.Children;

        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
                chosenSizes.AddRange(stack.ChosenSizes);
        }

        chosenSizes = [.. chosenSizes.Distinct()];

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

        SizeDisabledWarningIsOpen = IconSizes.Any(x => !x.IsEnabled);
    }

    private FileSavePicker CreateSavePicker()
    {
        FileSavePicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        savePicker.FileTypeChoices.Add("ICO File", [".ico"]);
        savePicker.DefaultFileExtension = ".ico";
        savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(ImagePath);

        if (!string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath))
        {
            try
            {
                Task.Run(async () =>
                {
                    StorageFile previousFile = await StorageFile.GetFileFromPathAsync(OutputPath);
                    savePicker.SuggestedSaveFile = previousFile;
                }).Wait();
            }
            catch { }
        }

        return savePicker;
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        ErrorInfoBarIsOpen = true;
    }

    private void UpdateUndoRedoState()
    {
        CanUndo = _undoRedo.CanUndo;
        CanRedo = _undoRedo.CanRedo;
    }

    public void StartCountdown()
    {
        if (!IsAutoRefreshEnabled)
            return;

        _countdownElapsedMs = 0;
        CountdownProgress = 0;
        IsCountdownActive = true;
        _countdownTimer.Start();
    }

    public void StopCountdown()
    {
        _countdownTimer.Stop();
        IsCountdownActive = false;
        CountdownProgress = 0;
        _countdownElapsedMs = 0;
    }

    private void OnCountdownTick(object? sender, ElapsedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _countdownElapsedMs += CountdownIntervalMs;

            CountdownProgress = (double)_countdownElapsedMs / CountdownDurationMs;

            if (_countdownElapsedMs >= CountdownDurationMs)
            {
                _countdownTimer.Stop();
                IsCountdownActive = false;
                CountdownProgress = 1.0;
                CountdownCompleted?.Invoke(this, EventArgs.Empty);
            }
        });
    }
}
