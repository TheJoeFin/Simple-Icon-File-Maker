using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Services;
using Windows.Services.Store;

namespace Simple_Icon_File_Maker;

public sealed partial class BuyProDialog : ContentDialog
{
    public BuyProDialog()
    {
        InitializeComponent();
    }

    private async void BuyProButton_Click(object sender, RoutedEventArgs e)
    {
        StorePurchaseStatus result = await StoreService.BuyPro();

        if (result is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
        {
            Hide();
            return;
        }
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PriceTextBlock.Text = await StoreService.ProPrice();
        BuyProButton.IsEnabled = true;
    }
}
