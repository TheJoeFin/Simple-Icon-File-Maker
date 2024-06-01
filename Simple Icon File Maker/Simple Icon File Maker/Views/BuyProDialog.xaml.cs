using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Simple_Icon_File_Maker.Services;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Services.Store;
using WinRT.Interop;

namespace Simple_Icon_File_Maker;

public sealed partial class BuyProDialog : ContentDialog
{
    public BuyProDialog()
    {
        InitializeComponent();

#if DEBUG
        licenseInformation = CurrentAppSimulator.LicenseInformation;
#else
        licenseInformation = CurrentApp.LicenseInformation;
#endif

        CheckPurchases();
    }

    private void CheckPurchases()
    {
        if (licenseInformation.ProductLicenses[proName].IsActive)
        {
            // the customer can access this feature
        }
        else
        {
            // the customer can' t access this feature
        }
    }

    private async void BuyProButton_Click(object sender, RoutedEventArgs e)
    {
        await BuyPro();
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        PriceTextBlock.Text = await StoreService.ProPrice();
        BuyProButton.IsEnabled = true;
    }

    public LicenseInformation licenseInformation { get; private set; }
    private readonly string proName = "pro-features";

    private async Task BuyPro()
    {
        if (licenseInformation.ProductLicenses[proName].IsActive == false)
        {
            try
            {
                // The customer doesn't own this feature, so
                // show the purchase dialog.
                StorePurchaseProperties proProps = new(proName);
                StoreContext store = StoreContext.GetDefault();
                var result = await store.GetAssociatedStoreProductsAsync(new string[] { "Durable", "Consumable" });
                if (result.ExtendedError is not null)
                {
                    throw new Exception("Failed to get items from store");
                }

                foreach (var item in result.Products)
                {
                    StoreProduct product = item.Value;

                    if (product.InAppOfferToken == proName)
                    {
                        // gets add-on
                        Window window = new();
                        IntPtr hwnd = WindowNative.GetWindowHandle(window);
                        InitializeWithWindow.Initialize(product, hwnd);
                        await product.RequestPurchaseAsync();
                    }
                }

                //Check the license state to determine if the in-app purchase was successful.
                if (licenseInformation.ProductLicenses[proName].IsActive)
                    Hide();
            }
            catch (Exception ex)
            {
                // The in-app purchase was not completed because
                // an error occurred.
                // FailedProPurchase.IsOpen = true;
                Debug.WriteLine(ex.Message);
            }
        }
        else
        {
            // The customer already owns this feature.
        }
    }
}
