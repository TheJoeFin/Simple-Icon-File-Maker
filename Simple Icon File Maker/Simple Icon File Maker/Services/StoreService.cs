using CommunityToolkit.WinUI.Helpers;
using Simple_Icon_File_Maker.Contracts.Services;
using System.Collections.Concurrent;
using System.Diagnostics;
using Windows.Services.Store;
using Windows.Storage;
using WinRT.Interop;

namespace Simple_Icon_File_Maker.Services;

public class StoreService : IStoreService
{
    private static readonly ConcurrentDictionary<string, StoreProduct> _productsCache = new();
    private static readonly ConcurrentDictionary<string, bool> _ownershipCache = new();
    private static StoreContext? _context;

    private const string proFeaturesId = "pro-features";

    public event EventHandler<string>? ProductPurchased;

    private bool? _ownsPro;

    public bool OwnsPro
    {
        get
        {
            if (_ownsPro is null)
                return false;

            return _ownsPro.Value;
        }
    }

    private string? _proPrice;

    public string ProPrice
    {
        get
        {
            if (_proPrice is null)
                return "---";

            return _proPrice;
        }
    }

    public StoreService()
    {
    }

    public async Task InitializeAsync()
    {
        _ownsPro = await ownsPro();
        _proPrice = await proPrice();
    }

    private async Task<bool> ownsPro()
    {
        bool ownsPro = false;
        string ownsProKey = "OwnsPro";
        try
        {
            ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
            bool settingExists = settings.Values.ContainsKey(ownsProKey);

            if (!settingExists)
            {
                ownsPro = await IsOwnedAsync(proFeaturesId);
                settings.Values[ownsProKey] = ownsPro;
            }
            else
            {
                bool previouslySetOwnedPro = (bool)settings.Values[ownsProKey];

                if (previouslySetOwnedPro)
                    return true;

                ownsPro = await IsOwnedAsync(proFeaturesId);
            }
        }
        catch (Exception ex)
        {
            ownsPro = await IsOwnedAsync(proFeaturesId);
            Debug.WriteLine(ex.Message);
        }

        return ownsPro;
    }

    public async Task<StorePurchaseStatus> BuyPro()
    {
        string ownsProKey = "OwnsPro";
        StorePurchaseStatus purchaseResult = await PurchaseAddOn(proFeaturesId);

        bool setSetting = false;

        switch (purchaseResult)
        {
            case StorePurchaseStatus.Succeeded:
                _ownsPro = true;
                setSetting = true;
                break;
            case StorePurchaseStatus.AlreadyPurchased:
                _ownsPro = true;
                setSetting = true;
                break;
            case StorePurchaseStatus.NotPurchased:
                break;
            case StorePurchaseStatus.NetworkError:
                break;
            case StorePurchaseStatus.ServerError:
                _ownsPro = true;
                setSetting = false;
                break;
            default:
                break;
        }

        if (setSetting)
        {
            try
            {
                ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
                settings.Values[ownsProKey] = _ownsPro;
            }
            catch (Exception)
            {
                throw;
            }
        }

        return purchaseResult;
    }

    private async Task<string> proPrice()
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

        foreach (KeyValuePair<string, StoreLicense> addOnLicense in appLicense.AddOnLicenses)
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

        if (result is StorePurchaseStatus.Succeeded or StorePurchaseStatus.AlreadyPurchased)
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

        InitializeWithWindow.Initialize(_context, App.MainWindow.WindowHandle);

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