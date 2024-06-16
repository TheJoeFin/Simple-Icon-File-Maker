using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Models;

namespace Simple_Icon_File_Maker.Services;

internal class IconSizesService : IIconSizesService
{
    public List<IconSize> IconSizes { get; private set; } = new();

    public Task InitializeAsync()
    {
        throw new NotImplementedException();
    }

    public Task Save(IEnumerable<IconSize> iconSizes)
    {
        throw new NotImplementedException();
    }
}
