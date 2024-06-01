using CommunityToolkit.WinUI.Helpers;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Services.Store;
using WinRT.Interop;

namespace Simple_Icon_File_Maker.Services;

public class StoreService
{
    private static readonly ConcurrentDictionary<string, StoreProduct> _productsCache = new();
    private static readonly ConcurrentDictionary<string, bool> _ownershipCache = new();
    private static StoreContext? _context;

    private const string proFeaturesId = "pro-features";

    public event EventHandler<string>? ProductPurchased;

    public static async Task<bool> OwnsPro()
    {
        return await IsOwnedAsync(proFeaturesId);
    }

    public static async Task<StorePurchaseStatus> BuyPro()
    {
        return await PurchaseAddOn(proFeaturesId);
    }

    public static async Task<string> ProPrice()
    {
        return await GetPriceAsync(proFeaturesId);
    }

    public static async Task<bool> IsOwnedAsync(string iapId)
    {
        if (_ownershipCache.TryGetValue(iapId, out bool isOwned))
            return isOwned;

        if (!NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            return false;

        _context ??= StoreContext.GetDefault();

        StoreAppLicense appLicense = await _context.GetAppLicenseAsync();
        if (appLicense is null)
            return false;

        foreach (var addOnLicense in appLicense.AddOnLicenses)
        {
            StoreLicense license = addOnLicense.Value;
            if (license.InAppOfferToken == iapId && license.IsActive)
            {
                // Handle add-on scenario
                _ownershipCache.TryAdd(iapId, true);
                return true;
            }
        }

        _ownershipCache.TryAdd(iapId, false);
        return false;
    }

    public static async Task<bool> IsAnyOwnedAsync(IReadOnlyList<string> iapIds)
    {
        if (iapIds.Count == 0)
            return true;

        foreach (string id in iapIds)
        {
            bool owned = await IsOwnedAsync(id);
            if (owned)
                return true;
        }

        return false;
    }

    public static async Task<string> GetPriceAsync(string iapId)
    {
        StoreProduct? addon = await GetAddOn(iapId);
        return addon?.Price?.FormattedPrice ?? "---";
    }

    public async Task<bool> BuyAsync(string iapId)
    {
        StorePurchaseStatus result = await PurchaseAddOn(iapId);

        if (result == StorePurchaseStatus.Succeeded || result == StorePurchaseStatus.AlreadyPurchased)
            _ownershipCache[iapId] = true;

        if (result == StorePurchaseStatus.Succeeded)
            ProductPurchased?.Invoke(this, iapId);

        return result switch
        {
            StorePurchaseStatus.Succeeded => true,
            StorePurchaseStatus.AlreadyPurchased => true,
            _ => false
        };
    }

    private static async Task<StorePurchaseStatus> PurchaseAddOn(string id)
    {
        if (!NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            return StorePurchaseStatus.NetworkError;

        StoreProduct? addOnProduct = await GetAddOn(id);
        if (addOnProduct is null)
            return StorePurchaseStatus.ServerError;

        Window window = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(window);
        InitializeWithWindow.Initialize(addOnProduct, hwnd);

        /// Attempt purchase
        StorePurchaseResult? result = await addOnProduct.RequestPurchaseAsync();
        if (result is null)
            return StorePurchaseStatus.ServerError;

        return result.Status;
    }

    private static async Task<StoreProduct?> GetAddOn(string id)
    {
        if (_productsCache.ContainsKey(id))
            return _productsCache[id];

        if (!NetworkHelper.Instance.ConnectionInformation.IsInternetAvailable)
            return null;

        _context ??= StoreContext.GetDefault();

        /// Get all add-ons for this app.
        StoreProductQueryResult result = await _context.GetAssociatedStoreProductsAsync(new string[] { "Durable", "Consumable" });
        if (result.ExtendedError is not null)
            return null;

        foreach (KeyValuePair<string, StoreProduct> item in result.Products)
        {
            StoreProduct product = item.Value;

            if (product.InAppOfferToken == id)
            {
                // gets add-on
                _productsCache.TryAdd(id, product);
                return product;
            }
        }

        return null;
    }
}