using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace Simple_Icon_File_Maker;

public sealed partial class EditSizesDialog : ContentDialog
{
    ObservableCollection<IconSize> IconSizes { get; set; } = new(IconSize.GetAllSizes());


    public EditSizesDialog()
    {
        InitializeComponent();

        SelectTheseIcons(IconSize.GetWindowsSizesFull());
    }

    private void CheckBox_Tapped(object sender, TappedRoutedEventArgs e)
    {
        
    }

    private void AddNewSizeButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        IconSize newSize = new((int)NewSizeNumberBox.Value);

        if (IconSizes.Contains(newSize))
            return;

        NewSizeNumberBox.Value = double.NaN;

        // insert into IconSizes in the right size order
        for (int i = 0; i < IconSizes.Count; i++)
        {
            if (IconSizes[i].SideLength < newSize.SideLength)
            {
                IconSizes.Insert(i, newSize);
                return;
            }
        }
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }
}
