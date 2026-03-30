using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Simple_Icon_File_Maker.Contracts.Services;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class AboutViewModel : ObservableRecipient
{
    private readonly IThemeSelectorService _themeSelectorService;

    public ElementTheme Theme => _themeSelectorService.Theme;

    [RelayCommand]
    public void GoBack()
    {
        NavigationService.GoBack();
    }

    private INavigationService NavigationService
    {
        get;
    }

    public AboutViewModel(INavigationService navigationService, IThemeSelectorService themeSelectorService)
    {
        NavigationService = navigationService;
        _themeSelectorService = themeSelectorService;
    }

    public async Task SwitchThemeAsync(ElementTheme theme)
    {
        await _themeSelectorService.SetThemeAsync(theme);
        OnPropertyChanged(nameof(Theme));
    }
}
