using System.ComponentModel;

namespace Simple_Icon_File_Maker.Models;

public class IconSize : INotifyPropertyChanged
{
    public int SideLength { get; set; }
    public bool IsSelected { get; set; } = true;

    public string Tooltip => $"{SideLength} x {SideLength}";

    public event PropertyChangedEventHandler? PropertyChanged;

    public static IconSize[] GetFullWindowsSizes()
    {
        return new IconSize[]
        {
            new() { SideLength = 256 },
            new() { SideLength = 128 },
            new() { SideLength = 72, IsSelected = false },
            new() { SideLength = 64 },
            new() { SideLength = 60, IsSelected = false },
            new() { SideLength = 40, IsSelected = false },
            new() { SideLength = 32 },
            new() { SideLength = 24, IsSelected = false },
            new() { SideLength = 20, IsSelected = false },
            new() { SideLength = 16 },
        };
    }
}
