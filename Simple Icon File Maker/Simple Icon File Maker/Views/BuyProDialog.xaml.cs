using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Contracts.Services;
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
        StorePurchaseStatus result = await App.GetService<IStoreService>().BuyPro();

        if (result is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
        {
            Hide();
            return;
        }
    }

    private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PriceTextBlock.Text = App.GetService<IStoreService>().ProPrice;
        BuyProButton.IsEnabled = true;
    }
}
