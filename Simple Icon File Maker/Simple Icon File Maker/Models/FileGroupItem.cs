using CommunityToolkit.Mvvm.ComponentModel;

namespace Simple_Icon_File_Maker.Models;

public partial class FileGroupItem : ObservableObject
{
    [ObservableProperty]
    public partial string Extension { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalCount { get; set; }

    [ObservableProperty]
    public partial int LargeFileCount { get; set; }

    [ObservableProperty]
    public partial bool IsIncluded { get; set; } = true;

    // Read-only label for CheckBox Content — built once, never mutates after creation
    public string DisplayLabel =>
        $"{Extension}  —  {TotalCount} file{(TotalCount == 1 ? "" : "s")}" +
        (LargeFileCount > 0 ? $"  ({LargeFileCount} over 5 MB)" : string.Empty);
}
