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

public partial class MultiViewModel : ObservableRecipient, INavigationAware
{
    private StorageFolder? _folder;

    public ObservableCollection<PreviewStack> Previews { get; } = [];

    public ObservableCollection<IconSize> IconSizes { get; set; } = [];

    // Reference to SizesControl
    public SizesControl? SizesControl { get; set; }

    private bool folderLoadCancelled = false;

    [ObservableProperty]
    public partial int FileLoadProgress { get; set; } = 0;

    [ObservableProperty]
    public partial bool LoadingImages { get; set; } = false;

    [ObservableProperty]
    public partial string FolderName { get; set; } = "Folder name";

    [ObservableProperty]
    public partial int NumberOfImageFiles { get; set; } = 0;

    [ObservableProperty]
    public partial int CurrentImageRendering { get; set; } = 0;

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
        if (_folder is null)
            return;

        string outputDirectory = _folder.Path;

        Uri uri = new(outputDirectory);
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

    public MultiViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public void OnNavigatedFrom()
    {
        folderLoadCancelled = true;
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is StorageFolder folder)
            _folder = folder;

        FolderName = _folder?.Path ?? "Folder path";
        if (FolderName.Length > 50) // truncate the text from the middle
            FolderName = string.Concat(FolderName.AsSpan(0, 20), "...", FolderName.AsSpan(FolderName.Length - 20));

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

    private async Task LoadFiles()
    {
        if (_folder is null || SizesControl == null)
            return;

        LoadingImages = true;
        Previews.Clear();

        Progress<int> progress = new();

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();
        NumberOfImageFiles = tempFiles.Count(x => x.IsSupportedImageFormat());

        FileLoadProgress = 0;
        CurrentImageRendering = 0;

        List<IconSize> sizes = SizesControl.ViewModel.IconSizes.Where(x => x.IsSelected && x.IsEnabled && !x.IsHidden).ToList();
        
        // Get current sort order
        IIconSizesService iconSizesService = App.GetService<IIconSizesService>();
        IconSortOrder sortOrder = iconSizesService.SortOrder;

        foreach (StorageFile file in tempFiles)
        {
            if (!file.IsSupportedImageFormat() || folderLoadCancelled)
                continue;

            CurrentImageRendering++;

            if (SkipIcoFiles && file.FileType == ".ico")
                continue;

            PreviewStack preview = new(file.Path, sizes, true)
            {
                MaxWidth = 600,
                MinWidth = 300,
                Margin = new Thickness(6),
                SortOrder = sortOrder
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
