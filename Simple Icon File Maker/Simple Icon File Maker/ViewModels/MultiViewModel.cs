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

    public ObservableCollection<StorageFile> Files { get; } = [];

    public ObservableCollection<PreviewStack> Previews { get; } = [];

    public ObservableCollection<IconSize> IconSizes { get; set; } = [];

    private bool folderLoadCancelled = false;

    [ObservableProperty]
    private int progress = 0;

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

        Progress<int> progress = new(percent =>
        {
            Progress = percent;
        });

        foreach (PreviewStack stack in Previews)
            await stack.GeneratePreviewImagesAsync(progress);

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
        foreach (PreviewStack stack in Previews)
            await stack.SaveIconAsync();
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

        // TODO order the icon frames by size and choose the largest size to compare
        // MagickImage image = new(ImagePath);
        // int smallerSide = (int)Math.Min(image.Width, image.Height);
        // 
        // foreach (IconSize size in IconSizes)
        //     size.IsEnabled = size.SideLength <= smallerSide;

        // SizeDisabledWarning.IsOpen = IconSizes.Any(x => !x.IsEnabled);
    }

    private async Task LoadFiles()
    {
        if (_folder is null)
            return;

        LoadingImages = true;
        Files.Clear();
        Previews.Clear();

        Progress<int> progress = new(percent =>
        {
            Progress = percent;
        });

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();

        List<IconSize> sizes = [..IconSize.GetWindowsSizesFull()];

        foreach (StorageFile file in tempFiles)
        {
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

        LoadingImages = false;
    }
}
