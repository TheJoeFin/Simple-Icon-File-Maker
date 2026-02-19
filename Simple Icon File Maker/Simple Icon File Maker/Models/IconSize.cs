using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Simple_Icon_File_Maker.Models;

[DebuggerDisplay("SideLength = {SideLength}, IsSelected = {IsSelected}")]
public partial class IconSize : ObservableObject, IEquatable<IconSize>
{
    [ObservableProperty]
    public partial int SideLength { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsHidden { get; set; } = false;

    [ObservableProperty]
    public partial int Order { get; set; } = 0;

    public string Tooltip => $"{SideLength} x {SideLength}";

    public IconSize()
    {

    }

    public override bool Equals(object? obj)
    {
        if (obj is not IconSize iconSize)
            return false;

        return Equals(iconSize);
    }

    public bool Equals(IconSize? other)
    {
        if (other?.SideLength == SideLength
            && other.IsSelected == IsSelected)
            return true;

        return false;
    }

    public IconSize(int sideLength)
    {
        SideLength = sideLength;
    }

    public IconSize(IconSize iconSize)
    {
        SideLength = iconSize.SideLength;
        IsSelected = iconSize.IsSelected;
        IsEnabled = iconSize.IsEnabled;
        Order = iconSize.Order;
    }

    public static IconSize[] GetAllSizes()
    {
        return
        [
            //new() { SideLength = 1024, IsSelected = false },
            //new() { SideLength = 512, IsSelected = false },
            new() { SideLength = 256 },
            new() { SideLength = 192, IsSelected = false },
            new() { SideLength = 180, IsSelected = false},
            new() { SideLength = 128 },
            new() { SideLength = 72, IsSelected = false},
            new() { SideLength = 64 },
            new() { SideLength = 60, IsSelected = false},
            new() { SideLength = 48, IsSelected = false },
            new() { SideLength = 40, IsSelected = false},
            new() { SideLength = 32 },
            new() { SideLength = 24, IsSelected = false},
            new() { SideLength = 20, IsSelected = false},
            new() { SideLength = 16 },
        ];
    }

    public static IconSize[] GetWindowsSizesFull()
    {
        return
        [
            new() { SideLength = 256 },
            new() { SideLength = 128 },
            new() { SideLength = 64 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        ];
    }

    public static IconSize[] GetIdealWebSizesFull()
    {
        return
        [
            new() { SideLength = 192 },
            new() { SideLength = 180 },
            new() { SideLength = 48 },
            new() { SideLength = 32 },
            new() { SideLength = 24 },
            new() { SideLength = 16 },
        ];
    }

    public static IconSize[] GetIdealWebSizesShort()
    {
        return
        [
            new() { SideLength = 48 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        ];
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash += 23 + (IsSelected ? 1 : 0);
        return hash * 23 + SideLength;
    }

    public static List<IconSize> SortSizes(IEnumerable<IconSize> sizes, IconSortOrder sortOrder)
    {
        return sortOrder switch
        {
            IconSortOrder.LargestFirst => [.. sizes.OrderByDescending(s => s.SideLength)],
            IconSortOrder.SmallestFirst => [.. sizes.OrderBy(s => s.SideLength)],
            _ => [.. sizes.OrderByDescending(s => s.SideLength)]
        };
    }
}

public class IconOrderComparer : IComparer<IconSize>
{
    public int Compare(IconSize? x, IconSize? y)
    {
        if (x is null && y is null) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        // First compare by Order property
        int orderComparison = x.Order.CompareTo(y.Order);
        if (orderComparison != 0)
            return orderComparison;

        // If Order is the same, fall back to size comparison
        return y.SideLength.CompareTo(x.SideLength); // Descending by default
    }
}

public class IconSideComparer : IEqualityComparer<IconSize>
{
    public bool Equals(IconSize? x, IconSize? y)
    {
        if (x is not IconSize iconX || y is not IconSize iconY)
            return false;

        bool isEqual = iconX.SideLength == iconY.SideLength;
        return isEqual;
    }

    public int GetHashCode([DisallowNull] IconSize obj)
    {
        return obj.GetHashCode();
    }
}
