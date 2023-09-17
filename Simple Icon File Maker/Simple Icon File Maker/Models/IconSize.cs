using System;
using System.ComponentModel;

namespace Simple_Icon_File_Maker.Models;

public class IconSize: INotifyPropertyChanged, IEquatable<IconSize>
{
    public int SideLength { get; set; }
    public bool IsSelected { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

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
        if (other?.SideLength == SideLength)
            return true;

        return false;
    }

    public IconSize(IconSize iconSize)
    {
        SideLength = iconSize.SideLength;
        IsSelected = iconSize.IsSelected;
        IsEnabled = iconSize.IsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static IconSize[] GetAllSizes()
    {
        return new IconSize[]
        {
            //new() { SideLength = 1024, IsSelected = false },
            //new() { SideLength = 512, IsSelected = false },
            new() { SideLength = 256 },
            new() { SideLength = 192 },
            new() { SideLength = 180},
            new() { SideLength = 128 },
            new() { SideLength = 72},
            new() { SideLength = 64 },
            new() { SideLength = 60},
            new() { SideLength = 48},
            new() { SideLength = 40},
            new() { SideLength = 32 },
            new() { SideLength = 24},
            new() { SideLength = 20},
            new() { SideLength = 16 },
        };
    }
    
    public static IconSize[] GetWindowsSizesFull()
    {
        return new IconSize[]
        {
            new() { SideLength = 256 },
            new() { SideLength = 128 },
            new() { SideLength = 64 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        };
    }

    public static IconSize[] GetIdealWebSizesFull()
    {
        return new IconSize[]
        {
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
        return new IconSize[]
        {
            //16x16, 32x32, 48x48
            new() { SideLength = 48 },
            new() { SideLength = 32 },
            new() { SideLength = 16 },
        };
    }
}
