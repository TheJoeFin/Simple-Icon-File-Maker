using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Helpers;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_Icon_File_Maker;

public sealed partial class EditSizesDialog : ContentDialog
{
    private ObservableCollection<IconSize> IconSizes { get; set; } = [];


    public EditSizesDialog()
    {
        InitializeComponent();
    }

    private void CheckBox_Tapped(object sender, TappedRoutedEventArgs e)
    {

    }

    private void AddNewSizeButton_Tapped(object sender, TappedRoutedEventArgs e)
    {
        IconSize newSize = new((int)NewSizeNumberBox.Value);

        // Check if a size with this SideLength already exists (ignore IsSelected state)
        IconSideComparer comparer = new();
        if (IconSizes.Any(size => comparer.Equals(size, newSize)))
            return;

        NewSizeNumberBox.Value = double.NaN;

        // insert into IconSizes in the right size order (largest first)
        for (int i = 0; i < IconSizes.Count; i++)
        {
            if (IconSizes[i].SideLength < newSize.SideLength)
            {
                IconSizes.Insert(i, newSize);
                return;
            }
        }

        // If we get here, the new size is the smallest, so add it at the end
        IconSizes.Add(newSize);
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

        SelectTheseIcons(IconSize.GetWindowsSizesFull());
    }
}
