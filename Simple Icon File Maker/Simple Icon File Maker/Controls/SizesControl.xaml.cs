using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.ViewModels;

namespace Simple_Icon_File_Maker.Controls;

public sealed partial class SizesControl : UserControl
{
    public SizesControlViewModel ViewModel { get; }

    public SizesControl()
    {
        InitializeComponent();
        ViewModel = App.GetService<SizesControlViewModel>();
        DataContext = ViewModel;
    }

    public void UpdateEnabledSizes(int smallerImageSide)
    {
        ViewModel.UpdateEnabledSizes(smallerImageSide);
    }

    public void ReloadIconSizes()
    {
        ViewModel.LoadIconSizes();
    }
}

