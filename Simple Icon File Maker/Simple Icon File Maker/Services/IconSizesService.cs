using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;

namespace Simple_Icon_File_Maker.Services;

public class IconSizesService : IIconSizesService
{
    public List<IconSize> IconSizes { get; private set; } = new();

    public IconSizesService()
    {
    }

    public async Task InitializeAsync()
    {
        IconSizes = await IconSizeHelper.GetIconSizes();
    }

    public async Task Save(IEnumerable<IconSize> iconSizes)
    {
        IconSizes = iconSizes.ToList();
        await IconSizeHelper.Save(IconSizes);
    }
}
