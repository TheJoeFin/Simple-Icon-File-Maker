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

    public ObservableCollection<UIElement> Previews { get; } = [];

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

    [RelayCommand]
    public void GoBack()
    {
        folderLoadCancelled = true;
        NavigationService.GoBack();
    }

    [RelayCommand]
    public void SelectAllSizes()
    {

    }

    [RelayCommand]
    public void DeselectAllSizes()
    {

    }

    [RelayCommand]
    public void SelectWindowsSizes()
    {

    }

    [RelayCommand]
    public void SelectWebSizes()
    {

    }

    [RelayCommand]
    public void EditIconSizes()
    {

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

        Progress<int> progress = new(percent =>
        {
            Progress = percent;
        });

        LoadIconSizes();
        await LoadFiles(progress);
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
        
    }

    private async Task LoadFiles(IProgress<int> progress)
    {
        if (_folder is null)
            return;

        LoadingImages = true;
        Files.Clear();

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();

        List<IconSize> sizes = [..IconSize.GetWindowsSizesFull()];

        foreach (StorageFile file in tempFiles)
        {
            if (!file.IsSupportedImageFormat() || folderLoadCancelled)
                continue;

            PreviewStack preview = new(file.Path, sizes)
            {
                MaxWidth = 600,
                MinWidth = 200,
                Margin = new Thickness(6)
            };

            Previews.Add(preview);
            bool successs = await preview.InitializeAsync(progress);
        }

        NumberOfImageFiles = Previews.Count;

        LoadingImages = false;
    }
}
