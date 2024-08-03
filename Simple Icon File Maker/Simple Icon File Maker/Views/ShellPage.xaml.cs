using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.ViewModels;

namespace Simple_Icon_File_Maker.Views;

public sealed partial class ShellPage : Page
{
    public ShellViewModel ViewModel
    {
        get;
    }

    public ShellPage(ShellViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();

        ViewModel.NavigationService.Frame = NavigationFrame;

        App.MainWindow.ExtendsContentIntoTitleBar = true;
        App.MainWindow.SetTitleBar(titleBar);
        App.MainWindow.Activated += MainWindow_Activated;
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        App.AppTitlebar = titleBar;
    }
}
