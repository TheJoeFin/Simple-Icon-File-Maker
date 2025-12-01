using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.ViewModels;

namespace Simple_Icon_File_Maker;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel { get; }

    public MainPage()
    {
        InitializeComponent();
        ViewModel = App.GetService<MainViewModel>();
        DataContext = ViewModel;

        // Set UI element references in ViewModel
        ViewModel.PreviewsGrid = PreviewsGrid;
        ViewModel.MainImage = MainImage;
        ViewModel.InitialLoadProgressBar = InitialLoadProgressBar;
        ViewModel.SizesControl = SizesControlInstance;

        ViewModel.CountdownCompleted += OnCountdownCompleted;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        // Wire up SizesControl events
        SizesControlInstance.ViewModel.SizeCheckboxTappedEvent += (s, e) => ViewModel.SizeCheckboxTapped();
        SizesControlInstance.ViewModel.EditIconSizesRequested += async (s, e) =>
        {
            await ViewModel.EditIconSizes();
            SizesControlInstance.ReloadIconSizes();
        };
        SizesControlInstance.ViewModel.SortOrderChanged += async (s, e) =>
        {
            await ViewModel.UpdatePreviewsSortOrder();
        };

        // Set initial state
        UpdateVisualState();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ViewModel.RefreshButtonIsAccent):
                RefreshButton.Style = ViewModel.RefreshButtonIsAccent
                    ? (Style)Application.Current.Resources["AccentButtonStyle"]
                    : (Style)Application.Current.Resources["DefaultButtonStyle"];
                break;

            case nameof(ViewModel.IsLoading):
            case nameof(ViewModel.IsImageSelected):
            case nameof(ViewModel.ImagePath):
                UpdateVisualState();
                break;
        }
    }

    private void UpdateVisualState()
    {
        // Determine which visual state to show based on ViewModel properties
        string stateName;

        if (ViewModel.IsLoading)
        {
            stateName = UiStates.ThinkingState.ToString();
        }
        else if (ViewModel.IsImageSelected)
        {
            stateName = UiStates.ImageSelectedState.ToString();
        }
        else if (string.IsNullOrWhiteSpace(ViewModel.ImagePath) || ViewModel.ImagePath == "-")
        {
            stateName = UiStates.WelcomeState.ToString();
        }
        else
        {
            stateName = UiStates.BlankState.ToString();
        }

        VisualStateManager.GoToState(this, stateName, true);
    }

    private async void OnCountdownCompleted(object? sender, EventArgs e)
    {
        await ViewModel.RefreshPreviews();
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ViewModel.UpdatePreviewsOnSizeChange();
    }

    private void Border_DragOver(object sender, DragEventArgs e)
    {
        ViewModel.HandleDragOver(e);
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        DragOperationDeferral def = e.GetDeferral();
        e.Handled = true;
        await ViewModel.HandleDrop(e);
        def.Complete();
    }

    private async void PasteFromClipboard_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.PasteFromClipboard();
    }

    private void MenuFlyout_Opening(object sender, object e)
    {
        bool ownsPro = ViewModel.CheckIfProFeatureAllowed();

        if (!ownsPro && sender is MenuFlyout flyout)
            flyout.Hide();
    }
}
