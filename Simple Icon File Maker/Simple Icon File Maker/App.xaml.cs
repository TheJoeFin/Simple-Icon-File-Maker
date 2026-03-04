using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Simple_Icon_File_Maker.Activation;
using Simple_Icon_File_Maker.Contracts.Services;
using Simple_Icon_File_Maker.Models;
using Simple_Icon_File_Maker.Services;
using Simple_Icon_File_Maker.ViewModels;
using Simple_Icon_File_Maker.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Simple_Icon_File_Maker;

public partial class App : Application
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static MainWindow MainWindow { get; } = new MainWindow();

    public static UIElement? AppTitlebar { get; set; }

    public static string? SharedImagePath { get; set; }

    public App()
    {
        InitializeComponent();

        Host = Microsoft.Extensions.Hosting.Host.
        CreateDefaultBuilder().
        UseContentRoot(AppContext.BaseDirectory).
        ConfigureServices((context, services) =>
        {
            // Default Activation Handler
            services.AddTransient<ActivationHandler<LaunchActivatedEventArgs>, DefaultActivationHandler>();

            // Other Activation Handlers

            // Services
            services.AddSingleton<ILocalSettingsService, LocalSettingsService>();
            services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
            services.AddSingleton<IActivationService, ActivationService>();
            services.AddSingleton<IPageService, PageService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IIconSizesService, IconSizesService>();
            services.AddSingleton<IStoreService, StoreService>();

            // Core Services
            services.AddSingleton<IFileService, FileService>();

            // Views and ViewModels
            services.AddTransient<ShellPage>();
            services.AddTransient<ShellViewModel>();
            services.AddTransient<MainPage>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<AboutPage>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<MultiPage>();
            services.AddTransient<MultiViewModel>();
            services.AddTransient<SizesControlViewModel>();

            // Configuration
            services.Configure<LocalSettingsOptions>(context.Configuration.GetSection(nameof(LocalSettingsOptions)));
        }).
        Build();

        UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // TODO: Log and handle exceptions as appropriate.
        // https://docs.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.application.unhandledexception.

        e.Handled = true;
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activatedEventArgs.Kind == ExtendedActivationKind.ShareTarget)
        {
            await HandleShareTargetActivationAsync(activatedEventArgs);
        }

        await App.GetService<IActivationService>().ActivateAsync(args);
    }

    private static async Task HandleShareTargetActivationAsync(AppActivationArguments activatedEventArgs)
    {
        if (activatedEventArgs.Data is Windows.ApplicationModel.Activation.IShareTargetActivatedEventArgs shareArgs)
        {
            ShareOperation shareOperation = shareArgs.ShareOperation;
            shareOperation.ReportStarted();

            try
            {
                if (shareOperation.Data.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> items = await shareOperation.Data.GetStorageItemsAsync();
                    foreach (IStorageItem item in items)
                    {
                        if (item is StorageFile file && Constants.FileTypes.SupportedImageFormats.Contains(file.FileType, StringComparer.OrdinalIgnoreCase))
                        {
                            StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
                            StorageFile copiedFile = await file.CopyAsync(tempFolder, file.Name, NameCollisionOption.GenerateUniqueName);
                            SharedImagePath = copiedFile.Path;
                            break;
                        }
                    }
                }
                else if (shareOperation.Data.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmapRef = await shareOperation.Data.GetBitmapAsync();
                    var stream = await bitmapRef.OpenReadAsync();

                    StorageFolder tempFolder = ApplicationData.Current.TemporaryFolder;
                    StorageFile tempFile = await tempFolder.CreateFileAsync("shared_image.png", CreationCollisionOption.GenerateUniqueName);

                    using (var outputStream = await tempFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await RandomAccessStream.CopyAsync(stream, outputStream);
                    }

                    SharedImagePath = tempFile.Path;
                }
            }
            catch (Exception)
            {
                // If share handling fails, continue with normal launch
            }

            shareOperation.ReportCompleted();
        }
    }

    public static string[]? cliArgs { get; } = Environment.GetCommandLineArgs();
}
