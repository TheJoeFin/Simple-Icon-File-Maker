using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Simple_Icon_File_Maker.Models;

[DebuggerDisplay("SideLength = {SideLength}, IsSelected = {IsSelected}")]
public class IconSize: INotifyPropertyChanged, IEquatable<IconSize>
{
    public int SideLength { get; set; }
    public bool IsSelected { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public bool IsHidden { get; set; } = false;

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
    }

#pragma warning disable CS0067 // The event 'IconSize.PropertyChanged' is never used
    public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067 // The event 'IconSize.PropertyChanged' is never used

    public static IconSize[] GetAllSizes()
    {
        return
        new IconSize[] {
            new() { SideLength = 1024, IsSelected = false },
            new() { SideLength = 512, IsSelected = false },
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
        };
    }
    
    public static IconSize[] GetWindowsSizesFull()
    {
        return
        new IconSize[] {
            new() { SideLength = 256 },
            new() { SideLength = 128 },
            new() { SideLength = 64 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        };
    }

    public static IconSize[] GetIdealWebSizesFull()
    {
        return
        new IconSize[] {
            new() { SideLength = 192 },
            new() { SideLength = 180 },
            new() { SideLength = 48 },
            new() { SideLength = 32 },
            new() { SideLength = 24 },
            new() { SideLength = 16 },
        };
    }

    public static IconSize[] GetIdealWebSizesShort()
    {
        return
        new IconSize[] {
            new() { SideLength = 48 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        };
    }

    public override int GetHashCode()
    {
        int hash = 17;
        hash += 23 + (IsSelected ? 1 : 0);
        return hash * 23 + SideLength;
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
