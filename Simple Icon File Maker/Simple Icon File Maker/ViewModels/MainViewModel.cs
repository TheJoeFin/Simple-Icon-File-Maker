using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Contracts.ViewModels;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using System.Timers;
using Microsoft.UI.Dispatching;

namespace Simple_Icon_File_Maker.ViewModels;

public partial class MainViewModel : ObservableRecipient, INavigationAware
{
    private readonly System.Timers.Timer _countdownTimer;
    private readonly DispatcherQueue _dispatcherQueue;
    private const int CountdownDurationMs = 3000; // 3 seconds
    private const int CountdownIntervalMs = 50; // Update every 50ms for smooth progress
    private int _countdownElapsedMs = 0;
    private System.Timers.Timer _settingsSaveTimer = new();

    [ObservableProperty]
    private bool isAutoRefreshEnabled = true;

    [ObservableProperty]
    private double countdownProgress = 0;

    [ObservableProperty]
    private bool isCountdownActive = false;

    public event EventHandler? CountdownCompleted;

    private readonly ILocalSettingsService _localSettingsService;

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

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

    INavigationService NavigationService
    {
        get;
    }

    public MainViewModel(INavigationService navigationService, ILocalSettingsService localSettingsService)
    {
        NavigationService = navigationService;
        _localSettingsService = localSettingsService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _countdownTimer = new System.Timers.Timer(CountdownIntervalMs);
        _countdownTimer.Elapsed += OnCountdownTick;
        _countdownTimer.AutoReset = true;

        _settingsSaveTimer.AutoReset = false; // Only save once after the interval
        _settingsSaveTimer.Interval = TimeSpan.FromMilliseconds(300).TotalMilliseconds;
        _settingsSaveTimer.Elapsed += SettingsSaveTimer_Elapsed;
    }

    private async void SettingsSaveTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        _settingsSaveTimer.Stop();

        await _localSettingsService.SaveSettingAsync<bool>(nameof(IsAutoRefreshEnabled), IsAutoRefreshEnabled);
    }

    public void OnNavigatedFrom()
    {
        StopCountdown();
        _countdownTimer.Dispose();
    }

    public async void OnNavigatedTo(object parameter)
    {
        try
        {
            IsAutoRefreshEnabled = await _localSettingsService.ReadSettingAsync<bool>(nameof(IsAutoRefreshEnabled));
        }
        catch (Exception)
        {
            IsAutoRefreshEnabled = true;
        }
    }

    public void StartCountdown()
    {
        if (!IsAutoRefreshEnabled)
            return;

        _countdownElapsedMs = 0;
        CountdownProgress = 0;
        IsCountdownActive = true;
        _countdownTimer.Start();
    }

    public void StopCountdown()
    {
        _countdownTimer.Stop();
        IsCountdownActive = false;
        CountdownProgress = 0;
        _countdownElapsedMs = 0;
    }

    private void OnCountdownTick(object? sender, ElapsedEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _countdownElapsedMs += CountdownIntervalMs;

            CountdownProgress = (double)_countdownElapsedMs / CountdownDurationMs;

            if (_countdownElapsedMs >= CountdownDurationMs)
            {
                _countdownTimer.Stop();
                IsCountdownActive = false;
                CountdownProgress = 1.0;
                CountdownCompleted?.Invoke(this, EventArgs.Empty);
            }
        });
    }
}
