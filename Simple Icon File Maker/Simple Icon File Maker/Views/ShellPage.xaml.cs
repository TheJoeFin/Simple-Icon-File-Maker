using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Contracts.Services;
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
        App.AppTitlebar = titleBar;

        if (App.GetService<IStoreService>().OwnsPro)
            titleBar.Subtitle += " Pro";
    }
}
