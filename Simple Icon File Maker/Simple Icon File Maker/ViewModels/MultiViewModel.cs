using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using System.Collections.ObjectModel;
using Windows.Storage;
using Simple_Icon_File_Maker.Constants;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MultiViewModel : ObservableRecipient, INavigationAware
{
    private StorageFolder? _folder;

    public ObservableCollection<StorageFile> Files { get; } = [];

    [RelayCommand]
    public void GoBack()
    {
        NavigationService.GoBack();
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
    }

    public async void OnNavigatedTo(object parameter)
    {
        if (parameter is StorageFolder folder)
            _folder = folder;

        await LoadFiles();
    }

    private async Task LoadFiles()
    {
        if (_folder is null)
            return;

        Files.Clear();

        IReadOnlyList<StorageFile> tempFiles = await _folder.GetFilesAsync();

        foreach (StorageFile file in tempFiles)
            if (file.IsSupportedImageFormat())
                Files.Add(file);
    }
}
