using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_Icon_File_Maker.Constants;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Simple_Icon_File_Maker.Controls;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.System;

namespace Simple_Icon_File_Maker.ViewModels;

public record MultiPageParameter(StorageFolder Folder, bool IsFromDllExtraction = false, string? SourceFilePath = null);

public partial class MultiViewModel : ObservableRecipient, INavigationAware
{
    private StorageFolder? _folder;
    private string? _sourceFilePath;
    private readonly ILocalSettingsService _localSettingsService;


    public ObservableCollection<PreviewStack> Previews { get; } = [];

    public ObservableCollection<IconSize> IconSizes { get; set; } = [];

    // Reference to SizesControl
    public SizesControl? SizesControl { get; set; }

    private bool folderLoadCancelled = false;
    private bool _skipPreCheck = false;
    private HashSet<string> _excludedExtensions = [];

    private const int PreCheckFileCountThreshold = 100;
    private const int PreCheckLargeFileCountThreshold = 5;
    private const ulong PreCheckLargeFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    [ObservableProperty]
    public partial int FileLoadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsCheckerBackgroundVisible { get; set; } = false;

    [ObservableProperty]
    public partial bool LoadingImages { get; set; } = false;

    [ObservableProperty]
    public partial string FolderName { get; set; } = "Folder name";

    [ObservableProperty]
    public partial int NumberOfImageFiles { get; set; } = 0;

    [ObservableProperty]
    public partial int CurrentImageRendering { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsAssessingFolder { get; set; } = false;

    [ObservableProperty]
    public partial bool ArePreviewsZoomed { get; set; } = false;

    [ObservableProperty]
    public partial bool IsRefreshNeeded { get; set; } = false;

    [ObservableProperty]
    public partial bool SkipIcoFiles { get; set; } = true;

    [ObservableProperty]
    public partial bool OverwriteFiles { get; set; } = false;

    [ObservableProperty]
    public partial bool SaveAllImagesAsPngs { get; set; } = false;

    [ObservableProperty]
    public partial int SizesGenerating { get; set; } = 0;

    [ObservableProperty]
    public partial bool IsFromDllExtraction { get; set; } = false;

    [ObservableProperty]
    public partial bool IsFolderMode { get; set; } = true;

    [ObservableProperty]
    public partial string FilesDescriptionSuffix { get; set; } = "\u00A0image files in the folder.";

    [ObservableProperty]
    public partial string CloseButtonText { get; set; } = "Close Folder";

    [ObservableProperty]
    public partial string OpenSourceTooltip { get; set; } = "Open folder...";

    partial void OnIsCheckerBackgroundVisibleChanged(bool value)
    {
        if (Previews is null || Previews.Count == 0)
            return;

        foreach (PreviewStack stack in Previews)
        {
            stack.ShowCheckerBackground = value;
            stack.UpdateSizeAndZoom();
        }

        _localSettingsService.SaveSettingAsync(nameof(IsCheckerBackgroundVisible), value);
    }

    [RelayCommand]
    public void GoBack()
    {
        folderLoadCancelled = true;
        NavigationService.GoBack();
    }

    [RelayCommand]
    public async Task EditIconSizes()
    {
        bool ownsPro = App.GetService<IStoreService>().OwnsPro;

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
    public async Task RegenPreviews()
    {
        if (SizesControl == null)
            return;

        LoadingImages = true;

        Progress<int> progress = new();

        // TODO: add the real progress indication
        FileLoadProgress = 0;
        CurrentImageRendering = 0;

        // Get current sort order and update all preview stacks
        IIconSizesService iconSizesService = App.GetService<IIconSizesService>();
        IconSortOrder sortOrder = iconSizesService.SortOrder;

        foreach (PreviewStack stack in Previews)
        {
            CurrentImageRendering++;
            FileLoadProgress = (int)((double)CurrentImageRendering / NumberOfImageFiles * 100);
            stack.SortOrder = sortOrder;
            await stack.GeneratePreviewImagesAsync(progress);
        }

        SizesGenerating = SizesControl.ViewModel.IconSizes.Count(x => x.IsSelected && x.IsEnabled && !x.IsHidden);

        LoadingImages = false;

        // Reset refresh needed state after regeneration is complete
        IsRefreshNeeded = false;
    }

    [RelayCommand]
    public void ZoomPreviews()
    {
        foreach (PreviewStack stack in Previews)
        {
            stack.IsZoomingPreview = ArePreviewsZoomed;
            stack.UpdateSizeAndZoom();
        }
    }

    public async Task UpdatePreviewsSortOrder()
    {
        // Get current sort order
        IIconSizesService iconSizesService = App.GetService<IIconSizesService>();
        IconSortOrder sortOrder = iconSizesService.SortOrder;

        // Update all preview stacks with new sort order and refresh displays
        foreach (PreviewStack stack in Previews)
        {
            stack.SortOrder = sortOrder;
            await stack.RefreshPreviewsWithSortOrder();
        }
    }

    [RelayCommand]
    public void SizeCheckbox_Tapped()
    {
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public async Task SaveAllIcons()
    {
        IIconSizesService iconSizesService = App.GetService<IIconSizesService>();
        IconSortOrder sortOrder = iconSizesService.SortOrder;

        if (SaveAllImagesAsPngs)
        {
            foreach (PreviewStack stack in Previews)
                await stack.SaveAllImagesAsync("", sortOrder);
        }
        else
        {
            foreach (PreviewStack stack in Previews)
                await stack.SaveIconAsync("", sortOrder);
        }
    }

    [RelayCommand]
    public async Task ReloadFiles()
    {
        await LoadFiles();
    }

    [RelayCommand]
    public async Task OpenFolder()
    {
        string? targetPath = IsFromDllExtraction && _sourceFilePath is not null
            ? Path.GetDirectoryName(_sourceFilePath)
            : _folder?.Path;

        if (string.IsNullOrEmpty(targetPath))
            return;

        Uri uri = new(targetPath);
        LauncherOptions options = new()
        {
            TreatAsUntrusted = false,
            DesiredRemainingView = Windows.UI.ViewManagement.ViewSizePreference.UseLess
        };

        _ = await Launcher.LaunchUriAsync(uri, options);
    }

    private INavigationService NavigationService
    {
        get;
    }

    public MultiViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        NavigationService = navigationService;
        _localSettingsService = localSettingsService;
    }

    public void OnNavigatedFrom()
    {
        folderLoadCancelled = true;
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is MultiPageParameter navParam)
        {
            _folder = navParam.Folder;
            IsFromDllExtraction = navParam.IsFromDllExtraction;
            IsFolderMode = !IsFromDllExtraction;
            _sourceFilePath = navParam.SourceFilePath;
        }
        else if (parameter is StorageFolder folder)
        {
            _folder = folder;
        }

        if (IsFromDllExtraction && _sourceFilePath is not null)
        {
            FolderName = Path.GetFileName(_sourceFilePath);
            FilesDescriptionSuffix = "\u00A0icons extracted.";
            CloseButtonText = "Close";
            OpenSourceTooltip = "Open containing folder...";
        }
        else
        {
            string path = _folder?.Path ?? "Folder path";
            FolderName = path.Length > 50
                ? string.Concat(path.AsSpan(0, 20), "...", path.AsSpan(path.Length - 20))
                : path;
            FilesDescriptionSuffix = "\u00A0image files in the folder.";
            CloseButtonText = "Close Folder";
            OpenSourceTooltip = "Open folder...";
        }

        _skipPreCheck = await _localSettingsService
            .ReadSettingAsync<bool>(PreCheckDialog.SkipPreCheckSettingKey);

        LoadIconSizes();
        await LoadFiles();
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        if (SizesControl == null) return;

        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in SizesControl.ViewModel.IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }

    private void LoadIconSizes()
    {
        // Delegate to SizesControl
        SizesControl?.ReloadIconSizes();
        CheckIfRefreshIsNeeded();
    }

    private void CheckIfRefreshIsNeeded()
    {
        if (SizesControl == null)
            return;

        bool anyRefreshAvailable = false;
        foreach (PreviewStack stack in Previews)
            _ = stack.ChooseTheseSizes(SizesControl.ViewModel.IconSizes);

        foreach (PreviewStack stack in Previews)
        {
            if (stack.CanRefresh)
            {
                anyRefreshAvailable = true;
                break;
            }
        }

        IsRefreshNeeded = anyRefreshAvailable;

        foreach (IconSize size in SizesControl.ViewModel.IconSizes)
            size.IsEnabled = true;

        IEnumerable<IconSize> sizes = SizesControl.ViewModel.IconSizes.Where(x => !x.IsHidden && x.IsSelected);
        int largestSize = sizes.Any() ? sizes.Max(x => x.SideLength) : 0;
        int smallestSource = 0;

        if (Previews.Count == 0)
        {
            SizesControl.ViewModel.SizeDisabledWarningIsOpen = false;
            return;
        }

        smallestSource = Previews.Min(x => x.SmallerSourceSide);

        SizesControl.ViewModel.SizeDisabledWarningIsOpen = smallestSource < largestSize;
    }

    private static async Task<List<FileGroupItem>> BuildFileGroupsAsync(
        IReadOnlyList<StorageFile> files)
    {
        StorageFile[] imageFiles = [.. files.Where(f => f.IsSupportedImageFormat())];
        Task<Windows.Storage.FileProperties.BasicProperties>[] propTasks =
            imageFiles.Select(f => f.GetBasicPropertiesAsync().AsTask()).ToArray();
        Windows.Storage.FileProperties.BasicProperties[] allProps =
            await Task.WhenAll(propTasks);

        Dictionary<string, FileGroupItem> groups = [];
        for (int i = 0; i < imageFiles.Length; i++)
        {
            string ext = imageFiles[i].FileType.ToLowerInvariant();
            if (!groups.TryGetValue(ext, out FileGroupItem? group))
            {
                group = new FileGroupItem { Extension = ext };
                groups[ext] = group;
            }
            group.TotalCount++;
            if (allProps[i].Size > PreCheckLargeFileSizeBytes)
                group.LargeFileCount++;
        }
        return [.. groups.Values.OrderBy(g => g.Extension)];
    }

    private static bool ShouldShowPreCheck(List<FileGroupItem> groups)
        => groups.Sum(g => g.TotalCount) > PreCheckFileCountThreshold
        || groups.Sum(g => g.LargeFileCount) > PreCheckLargeFileCountThreshold;

    private async Task LoadFiles()
    {
        if (_folder is null || SizesControl == null)
            return;

        LoadingImages = true;
        IsAssessingFolder = true;
        Previews.Clear();

        Progress<int> progress = new();

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();
        NumberOfImageFiles = tempFiles.Count(x => x.IsSupportedImageFormat());

        FileLoadProgress = 0;
        CurrentImageRendering = 0;
        IsAssessingFolder = false;
        _excludedExtensions = [];
        bool dialogWasShown = false;

        if (!_skipPreCheck)
        {
            List<FileGroupItem> groups = await BuildFileGroupsAsync(tempFiles);

            if (ShouldShowPreCheck(groups))
            {
                // Seed .ico default from the current SkipIcoFiles preference
                FileGroupItem? icoGroup = groups.FirstOrDefault(g =>
                    g.Extension.Equals(".ico", StringComparison.OrdinalIgnoreCase));
                if (icoGroup is not null)
                    icoGroup.IsIncluded = !SkipIcoFiles;

                PreCheckDialog dialog = new() { TotalImageCount = NumberOfImageFiles };
                foreach (FileGroupItem group in groups)
                    dialog.FileGroups.Add(group);

                _ = await NavigationService.ShowModal(dialog);
                dialogWasShown = true;

                if (!dialog.IsConfirmed)
                {
                    LoadingImages = false;
                    GoBack();
                    return;
                }

                _excludedExtensions = [.. dialog.FileGroups
                    .Where(g => !g.IsIncluded)
                    .Select(g => g.Extension)];
            }
        }

        // If the dialog was not shown, honour SkipIcoFiles via the exclusion set
        if (!dialogWasShown && SkipIcoFiles)
            _excludedExtensions = [.. _excludedExtensions.Append(".ico")];

        // Recalculate total to match what will actually be processed
        NumberOfImageFiles = tempFiles.Count(f =>
            f.IsSupportedImageFormat() &&
            !_excludedExtensions.Contains(f.FileType.ToLowerInvariant()));

        List<IconSize> sizes = [.. SizesControl.ViewModel.IconSizes.Where(x => x.IsSelected && x.IsEnabled && !x.IsHidden)];

        // Get current sort order
        IIconSizesService iconSizesService = App.GetService<IIconSizesService>();
        IconSortOrder sortOrder = iconSizesService.SortOrder;

        foreach (StorageFile file in tempFiles)
        {
            if (!file.IsSupportedImageFormat() || folderLoadCancelled)
                continue;

            if (_excludedExtensions.Contains(file.FileType.ToLowerInvariant()))
                continue;

            CurrentImageRendering++;

            PreviewStack preview = new(file.Path, sizes, true)
            {
                MaxWidth = 600,
                MinWidth = 300,
                Margin = new Thickness(6),
                SortOrder = sortOrder,
                ShowCheckerBackground = IsCheckerBackgroundVisible
            };

            Previews.Add(preview);
            FileLoadProgress = (int)((double)CurrentImageRendering / NumberOfImageFiles * 100);
            _ = await preview.InitializeAsync(progress);
        }

        NumberOfImageFiles = Previews.Count;
        SizesGenerating = SizesControl.ViewModel.IconSizes.Count(x => x.IsSelected && x.IsEnabled && !x.IsHidden);

        CheckIfRefreshIsNeeded();

        LoadingImages = false;
    }
}
