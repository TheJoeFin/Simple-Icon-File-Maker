using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{

    [RelayCommand]
    public void NavigateToAbout()
    {
        NavigationService.NavigateTo(typeof(AboutViewModel).FullName!);
    }

    [RelayCommand]
    public async Task NavigateToMulti()
    {
        FolderPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail,
            CommitButtonText = "Select",
            FileTypeFilter = { "*" }
        };

        Window saveWindow = new();
        IntPtr windowHandleSave = WindowNative.GetWindowHandle(saveWindow);
        InitializeWithWindow.Initialize(picker, windowHandleSave);

        StorageFolder folder = await picker.PickSingleFolderAsync();

        if (folder is not null)
            NavigationService.NavigateTo(typeof(MultiViewModel).FullName!, folder);
    }

    INavigationService NavigationService
    {
        get;
    }

    public MainViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public void OnNavigatedFrom()
    {
        
    }

    public void OnNavigatedTo(object parameter)
    {
        
    }
}
