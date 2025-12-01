using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;

namespace Simple_Icon_File_Maker.Services;

public class IconSizesService : IIconSizesService
{
    private readonly ILocalSettingsService _localSettingsService;
    private const string SortOrderKey = "IconSortOrder";

    public List<IconSize> IconSizes { get; private set; } = [];
    public IconSortOrder SortOrder { get; private set; } = IconSortOrder.LargestFirst;

    public IconSizesService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
    }

    public async Task InitializeAsync()
    {
        IconSizes = await IconSizeHelper.GetIconSizes();

        try
        {
            SortOrder = await _localSettingsService.ReadSettingAsync<IconSortOrder>(SortOrderKey);
        }
        catch
        {
            SortOrder = IconSortOrder.LargestFirst;
        }
    }

    public async Task Save(IEnumerable<IconSize> iconSizes)
    {
        IconSizes = iconSizes.ToList();
        await IconSizeHelper.Save(IconSizes);
    }

    public async Task SaveSortOrder(IconSortOrder sortOrder)
    {
        SortOrder = sortOrder;
        await _localSettingsService.SaveSettingAsync(SortOrderKey, sortOrder);
    }
}
