using System.ComponentModel;

namespace Simple_Icon_File_Maker.Models;

public record IconSize: INotifyPropertyChanged
{
    public int SideLength { get; set; }
    public bool IsSelected { get; set; } = true;

    public bool IsEnabled { get; set; } = true;

    public string Tooltip => $"{SideLength} x {SideLength}";

    public IconSize()
    {

    }

    public IconSize(IconSize iconSize)
    {
        SideLength = iconSize.SideLength;
        IsSelected = iconSize.IsSelected;
        IsEnabled = iconSize.IsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static IconSize[] GetFullWindowsSizes()
    {
        return new IconSize[]
        {
            //new() { SideLength = 1024, IsSelected = false },
            //new() { SideLength = 512, IsSelected = false },
            new() { SideLength = 256 },
            new() { SideLength = 128 },
            new() { SideLength = 72, IsSelected = false },
            new() { SideLength = 64 },
            new() { SideLength = 60, IsSelected = false },
            new() { SideLength = 48, IsSelected = false },
            new() { SideLength = 40, IsSelected = false },
            new() { SideLength = 32 },
            new() { SideLength = 24, IsSelected = false },
            new() { SideLength = 20, IsSelected = false },
            new() { SideLength = 16 },
        };
    }
}
