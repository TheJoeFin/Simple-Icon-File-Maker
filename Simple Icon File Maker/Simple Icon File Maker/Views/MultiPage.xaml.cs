using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.ViewModels;

namespace Simple_Icon_File_Maker.Views;

public sealed partial class MultiPage : Page
{
    MultiViewModel ViewModel { get; }


    public MultiPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<MultiViewModel>();
    }
}
