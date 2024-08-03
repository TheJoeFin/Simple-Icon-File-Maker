using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{

    [RelayCommand]
    public void NavigateToAbout()
    {
        NavigationService.NavigateTo(typeof(AboutViewModel).FullName!);
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
