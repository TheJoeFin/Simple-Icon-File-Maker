using CommunityToolkit.Mvvm.ComponentModel;

using Simple_Icon_File_Maker.Contracts.ViewModels;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    public MainViewModel()
    {
    }

    public void OnNavigatedFrom()
    {
        throw new NotImplementedException();
    }

    public void OnNavigatedTo(object parameter)
    {
        throw new NotImplementedException();
    }
}
