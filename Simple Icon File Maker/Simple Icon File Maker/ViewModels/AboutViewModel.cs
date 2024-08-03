using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class AboutViewModel : ObservableRecipient
{
    [RelayCommand]
    public void GoBack()
    {
        NavigationService.GoBack();
    }

    INavigationService NavigationService
    {
        get;
    }

    public AboutViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }
}
