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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        private readonly string _startupTracePath;
        private readonly List<string> _startupTrace;
        private readonly long _startupTimestamp;

        private bool IsFirstInstance { get; }
        public IServiceProvider Services { get; private set; }

        public App()
        {
            _startupTimestamp = Stopwatch.GetTimestamp();
            _startupTracePath = Environment.GetEnvironmentVariable("SINGLEPASS_STARTUP_TRACE");
            if (!string.IsNullOrWhiteSpace(_startupTracePath))
            {
                _startupTrace = new List<string>();
                MarkStartup("App constructor");
            }

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
            MarkStartup("Startup event");
            // Override culture
            //SinglePass.Language.Properties.Resources.Culture = new System.Globalization.CultureInfo("en-US");

            Constants.EnsurePaths();
            MarkStartup("Paths ready");
            var settings = AppSettingsService.Load(out var settingsReadError);
            MarkStartup("Settings loaded");
            ThemeService.ApplyTheme(settings.ThemeMode);
            MarkStartup("Theme applied");

            var preparation = new Lazy<Task<IServiceProvider>>(() => PrepareLoginAsync(settings, settingsReadError));
            var loginViewModel = new LoginWindowViewModel(
                () => preparation.Value, File.Exists(Constants.PasswordsFilePath));
            var loginWindow = new LoginWindow(loginViewModel);
            MarkStartup("Login window created");
            if (_startupTrace is not null)
            {
                loginWindow.SourceInitialized += (_, _) => MarkStartup("Login SourceInitialized");
                loginWindow.Loaded += (_, _) => MarkStartup("Login Loaded");
            }

            async void OnLoginContentRendered(object sender, EventArgs args)
            {
                loginWindow.ContentRendered -= OnLoginContentRendered;
                MarkStartup("Login ContentRendered");
                WriteStartupTrace();
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
            MarkStartup("Before ShowDialog");
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
            MarkStartup("Before configuration and DI");
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
            MarkStartup("Login ready");
            WriteStartupTrace();
            return Services;
        }

        private void MarkStartup(string stage)
        {
            if (_startupTrace is null)
                return;

            var elapsed = Stopwatch.GetElapsedTime(_startupTimestamp).TotalMilliseconds;
            _startupTrace.Add(elapsed.ToString("F3", CultureInfo.InvariantCulture) + "\t" + stage);
        }

        private void WriteStartupTrace()
        {
            if (_startupTrace is null)
                return;

            try
            {
                File.WriteAllLines(_startupTracePath, _startupTrace);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                Trace.TraceWarning("Could not write startup timings: {0}", ex.Message);
            }
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
