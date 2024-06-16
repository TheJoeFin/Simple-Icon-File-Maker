using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using Simple_Icon_File_Maker.Activation;
using Simple_Icon_File_Maker.Contracts.Services;

namespace Simple_Icon_File_Maker.Services;

public class ActivationService : IActivationService
{
    private readonly ActivationHandler<LaunchActivatedEventArgs> _defaultHandler;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IIconSizesService _iconSizesService;
    private readonly IStoreService _storeService;
    private UIElement? _shell = null;

    public ActivationService(ActivationHandler<LaunchActivatedEventArgs> defaultHandler,
                             IEnumerable<IActivationHandler> activationHandlers,
                             IThemeSelectorService themeSelectorService,
                             IStoreService storeService,
                             IIconSizesService iconSizesService)
    {
        _defaultHandler = defaultHandler;
        _activationHandlers = activationHandlers;
        _themeSelectorService = themeSelectorService;
        _storeService = storeService;
        _iconSizesService = iconSizesService;
    }

    public async Task ActivateAsync(object activationArgs)
    {
        // Execute tasks before activation.
        await InitializeAsync();

        if (App.m_window is not MainWindow mainWin)
            return;

        // Set the MainWindow Content.
        if (mainWin.ContentFrame.Content is null)
        {
            _shell = App.GetService<MainPage>();
            mainWin.ContentFrame.Content = _shell ?? new Frame();
        }

        // Handle activation via ActivationHandlers.
        await HandleActivationAsync(activationArgs);

        // Activate the MainWindow.
        App.m_window.Activate();

        // Execute tasks after activation.
        await StartupAsync();
    }

    private async Task HandleActivationAsync(object activationArgs)
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle(activationArgs));

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync(activationArgs);
        }

        if (_defaultHandler.CanHandle(activationArgs))
        {
            await _defaultHandler.HandleAsync(activationArgs);
        }
    }

    private async Task InitializeAsync()
    {
        await _themeSelectorService.InitializeAsync().ConfigureAwait(false);
        await _storeService.InitializeAsync().ConfigureAwait(true);
        await _iconSizesService.InitializeAsync().ConfigureAwait(false);
        await Task.CompletedTask;
    }

    private async Task StartupAsync()
    {
        await _themeSelectorService.SetRequestedThemeAsync();
        await Task.CompletedTask;
    }
}
