using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using System.Collections.ObjectModel;
using Windows.Storage;
using Simple_Icon_File_Maker.Constants;
using Microsoft.UI.Xaml;
using Simple_Icon_File_Maker.Controls;
using Simple_Icon_File_Maker.Models;
using Simple_Icon_File_Maker.Helpers;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MultiViewModel : ObservableRecipient, INavigationAware
{
    private StorageFolder? _folder;

    public ObservableCollection<PreviewStack> Previews { get; } = [];

    public ObservableCollection<IconSize> IconSizes { get; set; } = [];

    private bool folderLoadCancelled = false;

    [ObservableProperty]
    private int fileLoadProgress = 0;

    [ObservableProperty]
    private bool loadingImages = false;

    [ObservableProperty]
    private string folderName = "Folder name";

    [ObservableProperty]
    private int numberOfImageFiles = 0;

    [ObservableProperty]
    private bool arePreviewsZoomed = false;

    [ObservableProperty]
    private bool sizeDisabledWarningIsOpen = false;

    [ObservableProperty]
    private bool isRefreshNeeded = false;

    [ObservableProperty]
    private bool skipIcoFiles = true;

    [ObservableProperty]
    private bool overwriteFiles = false;

    [ObservableProperty]
    private bool saveAllImagesAsPngs = false;

    [ObservableProperty]
    private int sizesGenerating = 0;

    [RelayCommand]
    public void GoBack()
    {
        folderLoadCancelled = true;
        NavigationService.GoBack();
    }

    [RelayCommand]
    public void SelectAllSizes()
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = true;

        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public void DeselectAllSizes()
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
        LoadingImages = true;

        Progress<int> progress = new();

        // TODO: add the real progress indication
        FileLoadProgress = 0;
        int count = 0;

        foreach (PreviewStack stack in Previews)
        {
            count++;
            FileLoadProgress = (int)((double)count / NumberOfImageFiles * 100);
            await stack.GeneratePreviewImagesAsync(progress);
        }

        SizesGenerating = IconSizes.Count(x => x.IsSelected && x.IsEnabled && !x.IsHidden);

        LoadingImages = false;
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

    [RelayCommand]
    public void SizeCheckbox_Tapped()
    {
        CheckIfRefreshIsNeeded();
    }

    [RelayCommand]
    public async Task SaveAllIcons()
    {
        if (SaveAllImagesAsPngs)
        {
            foreach (PreviewStack stack in Previews)
                await stack.SaveAllImagesAsync();
        }
        else
        {
            foreach (PreviewStack stack in Previews)
                await stack.SaveIconAsync();
        }
    }

    [RelayCommand]
    public async Task ReloadFiles()
    {
        await LoadFiles();
    }

    INavigationService NavigationService
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

        FolderName = _folder?.DisplayName ?? "Folder name";

        LoadIconSizes();
        await LoadFiles();
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
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

    private void CheckIfRefreshIsNeeded()
    {
        bool anyRefreshAvailable = false;
        foreach (PreviewStack stack in Previews)
            _ = stack.ChooseTheseSizes(IconSizes);

        foreach (PreviewStack stack in Previews)
        {
            if (stack.CanRefresh)
            {
                anyRefreshAvailable = true;
                break;
            }
        }

        IsRefreshNeeded = anyRefreshAvailable;

        foreach (IconSize size in IconSizes)
            size.IsEnabled = true;

        int largestSize = IconSizes.Where(x => !x.IsHidden && x.IsSelected).Max(x => x.SideLength);
        int smallestSource = 0;

        if (Previews.Count == 0)
        {
            SizeDisabledWarningIsOpen = false;
            return;
        }

        smallestSource = Previews.Min(x => x.SmallerSourceSide);

        SizeDisabledWarningIsOpen = smallestSource < largestSize;
    }

    private async Task LoadFiles()
    {
        if (_folder is null)
            return;

        LoadingImages = true;
        Previews.Clear();

        Progress<int> progress = new();

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();
        NumberOfImageFiles = tempFiles.Count(x => x.IsSupportedImageFormat());

        FileLoadProgress = 0;
        int count = 0;

        List<IconSize> sizes = IconSizes.Where(x => x.IsSelected && x.IsEnabled && !x.IsHidden).ToList();

        foreach (StorageFile file in tempFiles)
        {
            count++;
            FileLoadProgress = (int)((double)count / NumberOfImageFiles * 100);

            if (!file.IsSupportedImageFormat() || folderLoadCancelled)
                continue;

            if (SkipIcoFiles && file.FileType == ".ico")
                continue;

            PreviewStack preview = new(file.Path, sizes)
            {
                MaxWidth = 600,
                MinWidth = 200,
                Margin = new Thickness(6)
            };

            Previews.Add(preview);
            _ = await preview.InitializeAsync(progress);
        }

        NumberOfImageFiles = Previews.Count;
        SizesGenerating = IconSizes.Count(x => x.IsSelected && x.IsEnabled && !x.IsHidden);

        CheckIfRefreshIsNeeded();

        LoadingImages = false;
    }
}
