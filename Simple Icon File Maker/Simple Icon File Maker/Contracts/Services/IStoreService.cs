using Windows.Services.Store;

namespace Simple_Icon_File_Maker.Contracts.Services;

public interface IStoreService
{
    event EventHandler<string>? ProductPurchased;

    Task<StorePurchaseStatus> BuyPro();

    Task InitializeAsync();

    bool OwnsPro { get; }

    string ProPrice { get; }
}