using Simple_Icon_File_Maker.Models;

namespace Simple_Icon_File_Maker.Contracts.Services;

public interface IIconSizesService
{
    List<IconSize> IconSizes { get; }
    IconSortOrder SortOrder { get; }
    Task Save(IEnumerable<IconSize> iconSizes);
    Task SaveSortOrder(IconSortOrder sortOrder);

    Task InitializeAsync();
}
