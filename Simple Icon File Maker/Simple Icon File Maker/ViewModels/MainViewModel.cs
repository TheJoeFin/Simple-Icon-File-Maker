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

public partial class MainViewModel : ObservableRecipient, INavigationAware, IDisposable
{
    private readonly System.Timers.Timer _countdownTimer;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private const int CountdownDurationMs = 3000; // 3 seconds
    private const int CountdownIntervalMs = 50; // Update every 50ms for smooth progress
    private const int SettingsSaveDelayMs = 300; // Delay before saving settings
    private const int UiInitializationDelayMs = 200; // Delay to allow UI to initialize
    private int _countdownElapsedMs = 0;
    private System.Timers.Timer _settingsSaveTimer = new();
    private readonly UndoRedo _undoRedo = new();
    private CancellationTokenSource? _loadImageCancellationTokenSource;
    private bool _disposed;

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
    public Controls.SizesControl? SizesControl { get; set; }

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
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(SettingsSaveDelayMs).TotalMilliseconds;
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
        await Task.Delay(UiInitializationDelayMs);
        if (!string.IsNullOrWhiteSpace(ImagePath))
        {
            await LoadFromImagePathAsync();
        }
    }

    public void OnNavigatedFrom()
    {
        _loadImageCancellationTokenSource?.Cancel();
        Dispose();
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
        // Cancel any ongoing load operation
        _loadImageCancellationTokenSource?.Cancel();
        _loadImageCancellationTokenSource = new CancellationTokenSource();

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
        await LoadFromImagePathAsync(_loadImageCancellationTokenSource.Token);
    }

    [RelayCommand]
    public async Task ClearImage()
    {
        try
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
            {
                if (file != null)
                    await file.DeleteAsync();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error clearing image: {ex.Message}");
            // Don't show error to user as clearing partially succeeded
        }
    }

    [RelayCommand]
    public void SizeCheckboxTapped()
    {
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void CancelCountdown()
    {
        StopCountdown();
        RefreshButtonIsAccent = false;
    }

    [RelayCommand]
    public async Task RefreshPreviews()
    {
        RefreshButtonIsAccent = false;
        StopCountdown();

        if (PreviewsGrid == null || SizesControl == null)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        Progress<int> progress = new(percent =>
        {
            LoadProgress = percent;
        });

        // Get current sort order and update all preview stacks
        IconSortOrder sortOrder = _iconSizesService.SortOrder;

        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                stack.SortOrder = sortOrder;
                await stack.GeneratePreviewImagesAsync(progress, ImagePath);
            }
        }

        bool isAnySizeSelected = SizesControl.ViewModel.IconSizes.Any(x => x.IsSelected);
        CanSave = isAnySizeSelected;
    }

    public async Task UpdatePreviewsSortOrder()
    {
        if (PreviewsGrid == null)
            return;

        UIElementCollection uIElements = PreviewsGrid.Children;

        // Get current sort order
        IconSortOrder sortOrder = _iconSizesService.SortOrder;

        // Update all preview stacks with new sort order and refresh displays
        foreach (UIElement element in uIElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                stack.SortOrder = sortOrder;
                await stack.RefreshPreviewsWithSortOrder();
            }
        }
    }

    [RelayCommand]
    public void ZoomPreviews(bool isZooming)
    {
        if (PreviewsGrid is null)
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
        try
        {
            FileSavePicker savePicker = CreateSavePicker();
            await FilePickerHelper.TrySetSuggestedFolderFromSourceImage(savePicker, ImagePath);
            InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file is null)
                return;

            OutputPath = file.Path;
            CanSave = false;

            IconSortOrder sortOrder = _iconSizesService.SortOrder;

            if (PreviewsGrid != null)
            {
                foreach (UIElement element in PreviewsGrid.Children)
                {
                    if (element is Controls.PreviewStack stack)
                        await stack.SaveIconAsync(OutputPath, sortOrder);
                }
            }

            OpenFolderButtonVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save icon: {ex.Message}");
            ShowError($"Failed to save icon: {ex.Message}");
        }
        finally
        {
            CanSave = true;
        }
    }

    [RelayCommand]
    public async Task SaveAllImages()
    {
        try
        {
            FileSavePicker savePicker = CreateSavePicker();
            await FilePickerHelper.TrySetSuggestedFolderFromSourceImage(savePicker, ImagePath);
            InitializeWithWindow.Initialize(savePicker, App.MainWindow.WindowHandle);

            StorageFile file = await savePicker.PickSaveFileAsync();

            if (file is null)
                return;

            OutputPath = file.Path;
            CanSave = false;

            IconSortOrder sortOrder = _iconSizesService.SortOrder;

            if (PreviewsGrid is not null)
            {
                foreach (UIElement element in PreviewsGrid.Children)
                {
                    if (element is Controls.PreviewStack stack)
                        await stack.SaveAllImagesAsync(OutputPath, sortOrder);
                }
            }

            OpenFolderButtonVisible = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to save all images: {ex.Message}");
            ShowError($"Failed to save all images: {ex.Message}");
        }
        finally
        {
            CanSave = true;
        }
    }

    [RelayCommand]
    public async Task OpenOutputFolder()
    {
        try
        {
            string? outputDirectory = Path.GetDirectoryName(OutputPath);
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                ShowError("Output folder path is not valid.");
                return;
            }

            Uri uri = new(outputDirectory);
            LauncherOptions options = new()
            {
                TreatAsUntrusted = false,
                DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseLess
            };

            bool success = await Launcher.LaunchUriAsync(uri, options);
            if (!success)
            {
                ShowError("Failed to open output folder. The folder may not exist.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open output folder: {ex.Message}");
            ShowError($"Failed to open output folder: {ex.Message}");
        }
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

        try
        {
            string newPath = await ImageHelper.ApplyGrayscaleAsync(ImagePath, MainImage);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
            _undoRedo.AddUndo(undoRedoItem);
            UpdateUndoRedoState();

            ImagePath = newPath;
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply grayscale: {ex.Message}");
            ShowError($"Failed to apply grayscale filter: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApplyBlackWhiteOtsu()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        try
        {
            string newPath = await ImageHelper.ApplyBlackWhiteOtsuAsync(ImagePath, MainImage);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
            _undoRedo.AddUndo(undoRedoItem);
            UpdateUndoRedoState();

            ImagePath = newPath;
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply black & white (OTSU): {ex.Message}");
            ShowError($"Failed to apply black & white filter: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApplyBlackWhiteKapur()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        try
        {
            string newPath = await ImageHelper.ApplyBlackWhiteKapurAsync(ImagePath, MainImage);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
            _undoRedo.AddUndo(undoRedoItem);
            UpdateUndoRedoState();

            ImagePath = newPath;
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply black & white (Kapur): {ex.Message}");
            ShowError($"Failed to apply black & white filter: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ApplyInvert()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
            return;

        try
        {
            string newPath = await ImageHelper.ApplyInvertAsync(ImagePath, MainImage);

            MagickImageUndoRedoItem undoRedoItem = new(MainImage!, ImagePath, newPath);
            _undoRedo.AddUndo(undoRedoItem);
            UpdateUndoRedoState();

            ImagePath = newPath;
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to apply invert: {ex.Message}");
            ShowError($"Failed to invert image colors: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Undo()
    {
        if (!_undoRedo.CanUndo)
            return;

        try
        {
            ImagePath = _undoRedo.Undo();
            UpdateUndoRedoState();
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to undo: {ex.Message}");
            ShowError($"Failed to undo: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task Redo()
    {
        if (!_undoRedo.CanRedo)
            return;

        try
        {
            ImagePath = _undoRedo.Redo();
            UpdateUndoRedoState();
            await RefreshPreviews();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to redo: {ex.Message}");
            ShowError($"Failed to redo: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task PasteFromClipboard()
    {
        // Cancel any ongoing load operation
        _loadImageCancellationTokenSource?.Cancel();
        _loadImageCancellationTokenSource = new CancellationTokenSource();

        try
        {
            IsLoading = true;
            ErrorInfoBarIsOpen = false;

            string? clipboardImagePath = await ClipboardHelper.TryGetImageFromClipboardAsync();

            if (clipboardImagePath != null)
            {
                ImagePath = clipboardImagePath;
                await LoadFromImagePathAsync(_loadImageCancellationTokenSource.Token);
            }
            else
            {
                ShowError("No image found in clipboard. Copy an image and try again.");
                IsLoading = false;
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected
            IsLoading = false;
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
        // Cancel any ongoing load operation
        _loadImageCancellationTokenSource?.Cancel();
        _loadImageCancellationTokenSource = new CancellationTokenSource();

        IsLoading = true;
        ErrorInfoBarIsOpen = false;
        ImagePath = string.Empty;

        try
        {
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

                await LoadFromImagePathAsync(_loadImageCancellationTokenSource.Token);
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
                await LoadFromImagePathAsync(_loadImageCancellationTokenSource.Token);
            }
            else if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                Debug.WriteLine("Dropped StorageItem");
                IReadOnlyList<IStorageItem> storageItems = await e.DataView.GetStorageItemsAsync();

                string? imagePath = await StorageItemHelper.TryGetImagePathFromStorageItems(storageItems);

                if (imagePath != null)
                {
                    ImagePath = imagePath;
                    await LoadFromImagePathAsync(_loadImageCancellationTokenSource.Token);
                }
                else
                {
                    List<string> failedItems = StorageItemHelper.GetFailedItemNames(storageItems);
                    ShowError(string.Join($",{Environment.NewLine}", failedItems));
                    IsLoading = false;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, this is expected
            IsLoading = false;
        }
    }

    public void HandleDragOver(DragEventArgs e)
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
        // Delegate to SizesControl
        SizesControl?.ReloadIconSizes();
        CheckIfRefreshIsNeeded();
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        if (SizesControl == null) return;

        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in SizesControl.ViewModel.IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }

    private void CheckIfRefreshIsNeeded()
    {
        StopCountdown();

        if (PreviewsGrid is null || SizesControl is null)
            return;

        UIElementCollection allElements = PreviewsGrid.Children;

        bool anyRefreshAvailable = false;
        foreach (UIElement element in allElements)
        {
            if (element is Controls.PreviewStack stack)
            {
                _ = stack.ChooseTheseSizes(SizesControl.ViewModel.IconSizes);

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
            SizesControl.UpdateEnabledSizes(smallerSide);
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

    private async Task LoadFromImagePathAsync(CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();

            MagickImage? image = await ImageHelper.LoadImageAsync(ImagePath);

            cancellationToken.ThrowIfCancellationRequested();

            if (image == null)
            {
                ShowError("Failed to load image");
                IsLoading = false;
                IsImageSelected = false;
                return;
            }

            MainImageSource = image.ToImageSource();
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, clean up and return
            IsLoading = false;
            IsImageSelected = false;
            return;
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            IsLoading = false;
            IsImageSelected = false;
            return;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (SizesControl == null)
                return;

            List<IconSize> selectedSizes = [.. SizesControl.ViewModel.IconSizes.Where(x => x.IsSelected)];
            Controls.PreviewStack previewStack = new(ImagePath, selectedSizes)
            {
                SortOrder = _iconSizesService.SortOrder
            };

            PreviewsGrid?.Children.Add(previewStack);

            Progress<int> progress = new(percent =>
            {
                LoadProgress = percent;
            });

            cancellationToken.ThrowIfCancellationRequested();

            bool generatedImages = await previewStack.InitializeAsync(progress);

            cancellationToken.ThrowIfCancellationRequested();

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
        }
        catch (OperationCanceledException)
        {
            // Operation was cancelled, clean up
            IsLoading = false;
            IsImageSelected = false;
            return;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void SelectIconSizesFromPreview()
    {
        if (PreviewsGrid == null || SizesControl == null)
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
            foreach (IconSize setSize in SizesControl.ViewModel.IconSizes)
            {
                if (setSize.SideLength == size.SideLength)
                {
                    isAlreadyInList = true;
                    setSize.IsSelected = true;
                    break;
                }
            }

            if (!isAlreadyInList)
                SizesControl.ViewModel.IconSizes.Add(size);
        }

        List<IconSize> orderedIcons = [.. SizesControl.ViewModel.IconSizes.OrderByDescending(size => size.SideLength)];
        SizesControl.ViewModel.IconSizes.Clear();

        foreach (IconSize size in orderedIcons)
            SizesControl.ViewModel.IconSizes.Add(size);

        int smallerSide = 0;

        if (Path.GetExtension(ImagePath).Equals(".ico", StringComparison.InvariantCultureIgnoreCase))
            smallerSide = SizesControl.ViewModel.IconSizes.First(x => x.IsSelected).SideLength;

        SizesControl.UpdateEnabledSizes(smallerSide);
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed || !disposing)
            return;

        // Dispose managed resources
        StopCountdown();
        _countdownTimer?.Dispose();
        _settingsSaveTimer?.Stop();
        _settingsSaveTimer?.Dispose();
        _loadImageCancellationTokenSource?.Cancel();
        _loadImageCancellationTokenSource?.Dispose();

        _disposed = true;
    }
}
