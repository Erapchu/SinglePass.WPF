using AsyncKeyedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using SinglePass.FavIcons.Application;
using SinglePass.FavIcons.Repository;
using SinglePass.WPF.Authorization.Brokers;
using SinglePass.WPF.Authorization.TokenHolders;
using SinglePass.WPF.Clouds.Services;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using SinglePass.WPF.Options;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views;
using SinglePass.WPF.Views.Windows;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SinglePass.WPF
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private readonly Mutex _mutex;

        private bool IsFirstInstance { get; }
        public IServiceProvider Services { get; private set; }

        public App()
        {
            _mutex = new Mutex(true, "SinglePass_CBD9AADE-1A82-48A2-9F7F-4F0EAAABEA30", out bool isFirstInstance);
            IsFirstInstance = isFirstInstance;

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            if (!IsFirstInstance)
            {
                // Show main window and shutdown process
                InterprocessHelper.ShowMainWindow();
                Environment.Exit(0);
            }
        }

        private static ServiceProvider ConfigureServices(IConfiguration configuration, AppSettings settings)
        {
            var services = new ServiceCollection();

            services.AddOptions();

            // NLog
            services.AddLogging(lb =>
            {
                lb.ClearProviders();
                lb.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                lb.AddNLog(configuration);
            });

            services.AddHttpClient();

            // Clouds
            // Google
            services.Configure<GoogleDriveConfig>(configuration.GetSection("Settings:GoogleDriveConfig"));
            services.AddTransient<GoogleOAuthProvider>();
            services.AddTransient<GoogleDriveTokenHolder>();
            services.AddTransient<GoogleDriveCloudService>();
            services.AddTransient<CryptoService>();
            services.AddSingleton<CloudServiceProvider>();

            // Windows
            services.AddScoped<MainWindow>();
            services.AddScoped<MainWindowViewModel>();
            services.AddScoped<PasswordsViewModel>();
            services.AddScoped<CloudSyncViewModel>();
            services.AddScoped<SettingsViewModel>();

            services.AddTransient<CredentialEditDialogWindow>();
            services.AddTransient<CredentialEditViewModel>();

            // Popup
            services.AddTransient<PopupWindow>();
            services.AddTransient<PopupViewModel>();

            // Main services
            services.AddSingleton<CredentialsCryptoService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton(settings);
            services.AddSingleton<AppSettingsService>();
            services.AddSingleton<SyncService>();
            services.AddSingleton<HotkeysService>();
            services.AddSingleton<ImageService>();
            services.AddSingleton<CredentialViewModelFactory>();
            services.AddSingleton<AddressBarExtractor>();
            services.AddSingleton(new AsyncKeyedLocker<string>(o =>
            {
                o.PoolSize = 20;
                o.PoolInitialFill = 1;
            }));

            // favicons
            services.AddSingleton<IFavIconCollector, FavIconCollector>();
            services.Configure<FavIconCacheOptions>(configuration.GetSection("FavIconCacheOptions"));
            services.AddScoped<FavIconCacheService>();
            services.AddScoped<IFavIconRepository, FavIconRepository>();
            services.AddDbContext<FavIconDbContext>((sp, options) => options.UseSqlite(sp.GetService<IOptions<FavIconCacheOptions>>().Value.ConnectionString));

            return services.BuildServiceProvider();
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Override culture
            //SinglePass.Language.Properties.Resources.Culture = new System.Globalization.CultureInfo("en-US");

            Constants.EnsurePaths();
            var settings = AppSettingsService.Load(out var settingsReadError);
            ThemeService.ApplyTheme(settings.ThemeMode);

            var preparation = new Lazy<Task<IServiceProvider>>(() => PrepareLoginAsync(settings, settingsReadError));
            var loginViewModel = new LoginWindowViewModel(
                () => preparation.Value, File.Exists(Constants.PasswordsFilePath));
            var loginWindow = new LoginWindow(loginViewModel);

            async void OnLoginContentRendered(object sender, EventArgs args)
            {
                loginWindow.ContentRendered -= OnLoginContentRendered;
                try
                {
                    await preparation.Value;
                }
                catch (Exception ex)
                {
                    Trace.TraceError("SinglePass startup failed: {0}", ex);
                    if (loginWindow.IsVisible)
                    {
                        MessageBox.Show(loginWindow, ex.Message, Constants.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                        loginWindow.Close();
                    }
                }
            }

            loginWindow.ContentRendered += OnLoginContentRendered;
            if (loginWindow.ShowDialog() != true)
            {
                Shutdown();
                return;
            }

            var hiw = new HiddenInterprocessWindow();
            var traySource = hiw.InitWithoutShowing();

            var mainWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;

            var trayIcon = new TrayIcon(traySource, mainWindow, Services.GetRequiredService<ILogger<TrayIcon>>());
            hiw.Closing += (_, _) => trayIcon.Dispose();
            mainWindow.Show();
        }

        private async Task<IServiceProvider> PrepareLoginAsync(AppSettings settings, Exception settingsReadError)
        {
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            Services = await Task.Run(() =>
            {
                var configuration = BuildConfiguration();
                var services = ConfigureServices(configuration, settings);
                try
                {
                    if (settingsReadError is not null)
                    {
                        services.GetRequiredService<ILogger<AppSettingsService>>()
                            .LogError(settingsReadError, "Failed to read settings from {Path}", Constants.CommonSettingsFilePath);
                    }

                    services.GetRequiredService<ILogger<LoginWindowViewModel>>();
                    services.GetRequiredService<CredentialsCryptoService>();
                    return services;
                }
                catch
                {
                    services.Dispose();
                    throw;
                }
            });
            return Services;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var logger = Services?.GetService<ILogger<App>>();
            logger?.LogInformation("The application is shutting down...{0}", Environment.NewLine);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = Services?.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "Dispatcher unhandled exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var logger = Services?.GetService<ILogger<App>>();
            logger?.LogError(e.ExceptionObject as Exception, "Domain unhandled exception");
        }
    }
}
