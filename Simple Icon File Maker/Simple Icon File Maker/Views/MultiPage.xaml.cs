using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.ViewModels;

namespace Simple_Icon_File_Maker.Views;

public sealed partial class MultiPage : Page
{
    public MultiViewModel ViewModel { get; }

    public MultiPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<MultiViewModel>();
        DataContext = ViewModel;

        // Set reference to SizesControl in ViewModel
        ViewModel.SizesControl = SizesControlInstance;

        // Wire up SizesControl events
        SizesControlInstance.ViewModel.SizeCheckboxTappedEvent += (s, e) => ViewModel.SizeCheckbox_Tapped();
        SizesControlInstance.ViewModel.EditIconSizesRequested += async (s, e) =>
        {
            await ViewModel.EditIconSizes();
            SizesControlInstance.ReloadIconSizes();
        };
        SizesControlInstance.ViewModel.SortOrderChanged += async (s, e) =>
        {
            await ViewModel.UpdatePreviewsSortOrder();
        };
    }
}

