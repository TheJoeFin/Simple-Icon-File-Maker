using Simple_Icon_File_Maker.Models;

namespace Simple_Icon_File_Maker.Contracts.Services;

internal interface IIconSizesService
{
    Task Save(IEnumerable<IconSize> iconSizes);

    Task InitializeAsync();
}
