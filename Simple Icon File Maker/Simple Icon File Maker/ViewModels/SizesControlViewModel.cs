using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Models;
using System.Collections.ObjectModel;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class SizesControlViewModel : ObservableObject
{
    private readonly IIconSizesService _iconSizesService;
    private readonly IStoreService _storeService;

    [ObservableProperty]
    private IconSortOrder currentSortOrder = IconSortOrder.LargestFirst;

    [ObservableProperty]
    private bool sizeDisabledWarningIsOpen;

    public ObservableCollection<IconSize> IconSizes { get; } = [];

    public event EventHandler? SizeCheckboxTappedEvent;
    public event EventHandler? EditIconSizesRequested;
    public event EventHandler? SortOrderChanged;

    public SizesControlViewModel(IIconSizesService iconSizesService, IStoreService storeService)
    {
        _iconSizesService = iconSizesService;
        _storeService = storeService;
        
        currentSortOrder = _iconSizesService.SortOrder;
        LoadIconSizes();
    }

    [RelayCommand]
    private void SizeCheckboxTapped()
    {
        SizeCheckboxTappedEvent?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ChangeSortOrder(IconSortOrder newSortOrder)
    {
        CurrentSortOrder = newSortOrder;
        await _iconSizesService.SaveSortOrder(newSortOrder);
        LoadIconSizes();
        SortOrderChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task EditIconSizes()
    {
        bool ownsPro = _storeService.OwnsPro;
        
        if (ownsPro)
        {
            EditIconSizesRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private void SelectWindowsSizes()
    {
        SelectTheseIcons(IconSize.GetWindowsSizesFull());
        OnSizeSelectionChanged();
    }

    [RelayCommand]
    private void SelectWebSizes()
    {
        SelectTheseIcons(IconSize.GetIdealWebSizesFull());
        OnSizeSelectionChanged();
    }

    [RelayCommand]
    private void SelectAllSizes()
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = true;

        OnSizeSelectionChanged();
    }

    [RelayCommand]
    private void ClearSizeSelection()
    {
        foreach (IconSize size in IconSizes)
            size.IsSelected = false;

        OnSizeSelectionChanged();
    }

    public void LoadIconSizes()
    {
        IconSizes.Clear();
        List<IconSize> loadedSizes = _iconSizesService.IconSizes;

        // Sort the sizes based on current sort order
        List<IconSize> sortedSizes = CurrentSortOrder switch
        {
            IconSortOrder.LargestFirst => [.. loadedSizes.OrderByDescending(s => s.SideLength)],
            IconSortOrder.SmallestFirst => [.. loadedSizes.OrderBy(s => s.SideLength)],
            _ => [.. loadedSizes.OrderByDescending(s => s.SideLength)]
        };

        foreach (IconSize size in sortedSizes)
        {
            if (!size.IsHidden)
                IconSizes.Add(size);
        }
    }

    public void UpdateEnabledSizes(int smallerImageSide)
    {
        foreach (IconSize size in IconSizes)
            size.IsEnabled = size.SideLength <= smallerImageSide;

        SizeDisabledWarningIsOpen = IconSizes.Any(x => !x.IsEnabled);
    }

    private void SelectTheseIcons(IconSize[] iconSizesToSelect)
    {
        IconSideComparer iconComparer = new();
        foreach (IconSize iconSize in IconSizes)
            iconSize.IsSelected = iconSizesToSelect.Contains(iconSize, iconComparer);
    }

    private void OnSizeSelectionChanged()
    {
        SizeCheckboxTappedEvent?.Invoke(this, EventArgs.Empty);
    }
}

