﻿using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Simple_Icon_File_Maker.Views;
using Microsoft.UI.Xaml.Media;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    [ObservableProperty]
    private string imagePath = string.Empty;

    [RelayCommand]
    public void NavigateToAbout()
    {
        NavigationService.NavigateTo(typeof(AboutViewModel).FullName!);
    }

    [RelayCommand]
    public async Task NavigateToMulti()
    {
        bool ownsPro = App.GetService<IStoreService>().OwnsPro;

        if (ownsPro)
        {
            FolderPicker picker = new()
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail,
                CommitButtonText = "Select",
                FileTypeFilter = { "*" }
            };

            InitializeWithWindow.Initialize(picker, App.MainWindow.WindowHandle);

            StorageFolder folder = await picker.PickSingleFolderAsync();

            if (folder is not null)
                NavigationService.NavigateTo(typeof(MultiViewModel).FullName!, folder);
        }
        else
        {
            BuyProDialog buyProDialog = new();
            _ = await NavigationService.ShowModal(buyProDialog);
        }
    }

    [RelayCommand]
    public async Task EditImage()
    {
        ImageEditDialog imageEditDialog = new(imagePath) { XamlRoot = App.MainWindow.Content.XamlRoot };
        await imageEditDialog.ShowAsync();
    }

    INavigationService NavigationService
    {
        get;
    }

    public MainViewModel(INavigationService navigationService)
    {
        NavigationService = navigationService;
    }

    public void OnNavigatedFrom()
    {
        
    }

    public void OnNavigatedTo(object parameter)
    {
        
    }
}
