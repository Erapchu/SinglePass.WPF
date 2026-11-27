using AsyncKeyedLock;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Extensions.Logging;
using Erapkey.FavIcons.Application;
using Erapkey.FavIcons.Repository;
using Erapkey.Core.Authorization.Brokers;
using Erapkey.Core.Authorization.TokenHolders;
using Erapkey.Core.Clouds.Services;
using Erapkey.Desktop.Extensions;
using Erapkey.Desktop.Helpers;
using Erapkey.Desktop.Hotkeys;
using Erapkey.Core.Localization;
using Erapkey.Core.Options;
using Erapkey.Desktop.Services;
using Erapkey.Desktop.Services.WindowsHello;
using Erapkey.Desktop.Settings;
using Erapkey.Desktop.ViewModels;
using Erapkey.Desktop.ViewModels.Dialogs;
using Erapkey.Desktop.Views.Helpers;
using Erapkey.Desktop.Views;
using Erapkey.Desktop.Views.Windows;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Erapkey.Core.Helpers;
using Erapkey.Core.Services;
using Erapkey.Core.Services.Browser;

namespace Erapkey.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private readonly Mutex _mutex;
        private ServiceProvider _rootServices;
        private IServiceScope _vaultScope;
        private MainWindow _vaultWindow;
        private LoginWindow _loginWindow;
        private bool _locking;
        private bool _updatePrompted;

        private bool IsFirstInstance { get; }
        public IServiceProvider Services { get; private set; }

        public App()
        {
            _mutex = new Mutex(true, Constants.SingleInstanceMutexName, out bool isFirstInstance);
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
            services.AddSingleton<IUserDataProtector, DpapiUserDataProtector>();

            // Clouds
            // Google
            services.Configure<GoogleDriveConfig>(configuration.GetSection("Settings:GoogleDriveConfig"));
            services.AddHttpClient(nameof(OAuthProvider)).RemoveAllLoggers();
            services.AddTransient<GoogleOAuthProvider>();
            services.AddSingleton<GoogleDriveTokenHolder>();
            services.AddSingleton<GoogleDriveCloudService>();

            services.Configure<YandexDiskConfig>(configuration.GetSection("Settings:YandexDiskConfig"));
            services.AddHttpClient(YandexDiskCloudService.HttpClientName).RemoveAllLoggers();
            services.AddSingleton<YandexOAuthProvider>();
            services.AddSingleton<YandexDiskTokenHolder>();
            services.AddSingleton<YandexDiskCloudService>();
            services.AddTransient<CryptoService>();
            services.AddSingleton<CloudServiceProvider>();

            // Windows
            services.AddScoped<MainWindow>();
            services.AddScoped<MainWindowViewModel>();
            services.AddScoped<PasswordsViewModel>();
            services.AddScoped<CloudSyncViewModel>();
            services.AddScoped<SettingsViewModel>();
            services.AddScoped<TrashViewModel>();

            // Popup
            services.AddTransient<PopupWindow>();
            services.AddTransient<PopupViewModel>();

            // Main services
            services.AddScoped<CredentialsCryptoService>();
            services.AddSingleton<ThemeService>();
            services.AddSingleton(settings);
            services.AddSingleton<AppSettingsService>();
            services.AddScoped<SyncService>();
            services.AddSingleton<HotkeysService>();
            services.AddSingleton<ImageService>();
            services.AddScoped<CredentialViewModelFactory>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<WindowsHelloUnlock>();
            services.AddSingleton<IVaultKeyObserver>(sp => sp.GetRequiredService<WindowsHelloUnlock>());

            // SSH agent: lives as long as the application and finds the vault of the current unlock itself.
            services.AddSingleton<SshAgentKeyStore>();
            services.AddSingleton<ISshAgentKeyStore>(sp => sp.GetRequiredService<SshAgentKeyStore>());
            services.AddSingleton<SshAgent>();
            services.AddSingleton<SshAgentServer>();

            // Browser extension: the pipe for Erapkey.NativeHost.exe, see docs/browser-extension.md.
            services.AddSingleton(sp => new BrowserPairingStore(
                Path.Combine(Constants.LocalAppDataDirectoryPath, "browserPairings.dat"), sp.GetRequiredService<IUserDataProtector>(),
                sp.GetRequiredService<ILogger<BrowserPairingStore>>()));
            services.AddSingleton<IBrowserBackend, BrowserBackend>();
            services.AddSingleton<BrowserPipeServer>();
            services.AddSingleton<NativeMessagingRegistration>();
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
                // OAuth client credentials; not in git, generated by CI from repository secrets.
                .AddJsonFile("settings.secrets.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                // Logs and the favicon cache go to the data folder of this instance (Erapkey or Erapkey.Dev).
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["NLog:variables:logDir"] = Constants.LogsDirectoryPath,
                    ["FavIconCacheOptions:ConnectionString"] = "Data Source=" + Path.Combine(Constants.CacheDirectoryPath, "FavIcon.db"),
                })
                .Build();
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            Constants.EnsurePaths();
            var settings = AppSettingsService.Load(out var settingsReadError);
            LocalizationManager.Instance.Apply(settings.Language);
            ThemeService.ApplyTheme(settings.ThemeMode);

            var preparation = new Lazy<Task<IServiceProvider>>(() => PrepareLoginAsync(settings, settingsReadError));
            var loginViewModel = new LoginWindowViewModel(
                () => preparation.Value, File.Exists(Constants.PasswordsFilePath), WindowsHelloUnlock.IsOfferedFor(settings));
            var loginWindow = new LoginWindow(loginViewModel);
            _loginWindow = loginWindow;

            var hiw = new HiddenInterprocessWindow(ShowVault);
            var traySource = hiw.InitWithoutShowing();
            var autoLock = new VaultAutoLock(traySource, settings, LockVault);
            hiw.Closing += (_, _) => autoLock.Dispose();

            async void OnLoginContentRendered(object sender, EventArgs args)
            {
                loginWindow.ContentRendered -= OnLoginContentRendered;
                try
                {
                    await preparation.Value;
                    // Before the unlock: the extension can pair and learn that the vault is locked.
                    if (settings.BrowserIntegrationEnabled)
                        StartBrowserIntegration(_rootServices);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Erapkey startup failed: {0}", ex);
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

            _loginWindow = null;
            var trayIcon = new TrayIcon(traySource, ShowVault, _rootServices.GetRequiredService<ILogger<TrayIcon>>());
            hiw.Closing += (_, _) => trayIcon.Dispose();

            var updates = _rootServices.GetRequiredService<UpdateService>();
            updates.UpdateReady += (_, _) => PromptUpdate();
            updates.Start();

            if (settings.SshAgentEnabled)
                StartSshAgent(_rootServices);

            OpenVault();
        }

        private void OpenVault()
        {
            _vaultWindow = Services.GetRequiredService<MainWindow>();
            MainWindow = _vaultWindow;
            _vaultWindow.Show();
            _rootServices.GetRequiredService<BrowserPipeServer>().NotifyUnlocked();
            Dispatcher.BeginInvoke(PromptUpdate, DispatcherPriority.ApplicationIdle);
        }

        /// <summary>Brings the vault forward with the entry selected; asks to unlock first when needed.</summary>
        internal void ShowEntry(Guid entryId)
        {
            ShowVault();
            _vaultWindow?.SelectCredential(entryId);
        }

        /// <summary>Registers the native messaging host and starts the pipe for it.</summary>
        internal static void StartBrowserIntegration(IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<App>>();
            if (!services.GetRequiredService<NativeMessagingRegistration>().Register())
                return;

            try
            {
                services.GetRequiredService<BrowserPipeServer>().Start();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "The browser server could not start");
            }
        }

        /// <summary>Offers to restart once a downloaded update is ready; asks at most once per session.</summary>
        private async void PromptUpdate()
        {
            var updates = _rootServices?.GetService<UpdateService>();
            if (_updatePrompted || updates?.State != UpdateState.ReadyToRestart)
                return;
            if (_vaultWindow is not { CanShowModal: true })
                return;

            _updatePrompted = true;
            var localization = LocalizationManager.Instance;
            var choice = await MessageDialog.ShowAsync(
                localization["UpdateAvailableHeader"],
                string.Format(localization.Culture, localization["UpdateAvailablePrompt"], updates.AvailableVersion),
                MessageDialogButtons.YesNo);
            if (choice == DialogChoice.Yes)
                updates.Restart();
        }

        /// <summary>The vault service of the current unlock, or null while the vault is locked.</summary>
        internal CredentialsCryptoService UnlockedVault =>
            _vaultWindow != null && !_locking ? Services?.GetService<CredentialsCryptoService>() : null;

        /// <summary>Shows the unlock window, or brings the vault forward, without waiting for it.</summary>
        internal void RequestUnlock() => Dispatcher.BeginInvoke(ShowVault, DispatcherPriority.Normal);

        private static void StartSshAgent(IServiceProvider services)
        {
            try
            {
                services.GetRequiredService<SshAgentServer>().Start();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                services.GetRequiredService<ILogger<App>>().LogError(ex, "The SSH agent could not start");
            }
        }

        internal bool ShowUnlockIfLocked()
        {
            if (_vaultWindow != null)
                return false;

            ShowVault();
            return true;
        }

        private void ShowVault()
        {
            if (_locking)
                return;

            if (_vaultWindow != null)
            {
                _vaultWindow.BringToFrontAndActivate();
                return;
            }

            if (_loginWindow != null)
            {
                _loginWindow.Activate();
                return;
            }

            _vaultScope = _rootServices.CreateScope();
            Services = _vaultScope.ServiceProvider;
            var login = new LoginWindow(new LoginWindowViewModel(() => Task.FromResult(Services), true,
                _rootServices.GetRequiredService<WindowsHelloUnlock>().IsOffered));
            _loginWindow = login;
            var accepted = login.ShowDialog() == true;
            _loginWindow = null;
            if (accepted)
                OpenVault();
            else
            {
                _vaultScope.Dispose();
                _vaultScope = null;
                Services = null;
            }
        }

        private async void LockVault()
        {
            if (_vaultWindow == null)
            {
                _loginWindow?.ResetForSessionLock();
                return;
            }

            _locking = true;
            var window = _vaultWindow;
            _vaultWindow = null;
            var scope = _vaultScope;
            _vaultScope = null;
            Services = null;
            var clearVault = scope.ServiceProvider.GetRequiredService<CredentialsCryptoService>().LockAsync();
            _rootServices.GetRequiredService<BrowserPipeServer>().NotifyLocked();
            WindowsClipboard.ClearIfOwned();
            try
            {
                scope.ServiceProvider.GetService<SyncService>()?.CancelTransfers();
                foreach (var popup in Windows.OfType<PopupWindow>().ToArray())
                    if (!popup.IsClosed)
                        popup.Close();

                window.CloseForVaultLock();
                MainWindow = null;
                await clearVault;
            }
            finally
            {
                scope.Dispose();
                _locking = false;
            }
        }

        private async Task<IServiceProvider> PrepareLoginAsync(AppSettings settings, Exception settingsReadError)
        {
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
            _rootServices = await Task.Run(() =>
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
                    _ = services.GetRequiredService<GoogleDriveTokenHolder>().OAuthInfo;
                    return services;
                }
                catch
                {
                    services.Dispose();
                    throw;
                }
            });
            _vaultScope = _rootServices.CreateScope();
            Services = _vaultScope.ServiceProvider;
            return Services;
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            WindowsClipboard.ClearIfOwned();
            _rootServices?.GetService<SshAgentServer>()?.Stop();
            _rootServices?.GetService<BrowserPipeServer>()?.Stop();
            var logger = _rootServices?.GetService<ILogger<App>>();
            logger?.LogInformation("The application is shutting down...{0}", Environment.NewLine);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var logger = _rootServices?.GetService<ILogger<App>>();
            logger?.LogError(e.Exception, "Dispatcher unhandled exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var logger = _rootServices?.GetService<ILogger<App>>();
            logger?.LogError(e.ExceptionObject as Exception, "Domain unhandled exception");
        }
    }
}
