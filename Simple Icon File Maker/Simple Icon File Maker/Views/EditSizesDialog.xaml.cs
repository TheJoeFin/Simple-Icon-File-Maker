using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Simple_Icon_File_Maker;

public sealed partial class EditSizesDialog : ContentDialog
{
    ObservableCollection<IconSize> IconSizes { get; set; } = new();


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

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        await App.GetService<IIconSizesService>().Save(IconSizes);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuFlyoutItem flyoutItem)
            return;

        if (flyoutItem.DataContext is IconSize iconSize)
            IconSizes.Remove(iconSize);
    }

    private async void ContentDialog_Loaded(object sender, RoutedEventArgs e)
    {
        List<IconSize> loadedSizes = await IconSizeHelper.GetIconSizes();

        foreach (IconSize size in loadedSizes)
            IconSizes.Add(size);
    }
}
