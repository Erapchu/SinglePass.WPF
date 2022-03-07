﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using PasswordManager.Authorization.Providers;
using PasswordManager.Authorization.Services;
using PasswordManager.Controls;
using PasswordManager.Services;
using PasswordManager.ViewModels;
using PasswordManager.Views;
using System;
using System.Windows;

namespace PasswordManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IConfiguration _configuration;

        private IHost _host;
        private Logger _logger;
        private TrayIcon _trayIcon;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.Exception, "Dispatcher unhandled exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.ExceptionObject as Exception, "Domain unhandled exception");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeComponent();

            // Welcome window
            var welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show();

            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            _logger = LogManager.Setup()
                .LoadConfigurationFromSection(_configuration)
                .GetCurrentClassLogger();

            _host = CreateHostBuilder().Build();
            _logger.Info("Log session started!");

            // Resolve theme
            var themeService = _host.Services.GetService<ThemeService>();
            themeService.Init();

            // Create tray icon
            _trayIcon = new TrayIcon();

            // Login
            using (var loginScope = _host.Services.CreateScope())
            {
                var loginWindow = _host.Services.GetService<LoginWindow>();
                welcomeWindow.Close();
                bool? dialogResult = loginWindow.ShowDialog(); // Stop here

                if (dialogResult != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Open main window
            var mainWindow = _host.Services.GetService<MainWindow>();
            mainWindow.Show();
        }

        private IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                // NLog
                services.AddLogging(lb =>
                {
                    lb.ClearProviders();
                    lb.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
                    lb.AddNLog(_configuration);
                });

                services.Configure<GoogleDriveConfig>(_configuration.GetSection("Settings:GoogleDriveConfig"));

                services.AddTransient<GoogleAuthorizationBroker>();

                services.AddScoped<LoginWindow>();
                services.AddScoped<LoginWindowViewModel>();

                services.AddScoped<MainWindow>();
                services.AddScoped<MainWindowViewModel>();
                services.AddScoped<PasswordsViewModel>();
                services.AddScoped<SettingsViewModel>();
                services.AddScoped<CredentialsDialogViewModel>();

                services.AddSingleton<CredentialsCryptoService>();
                services.AddSingleton<ThemeService>();
                services.AddSingleton<AppSettingsService>();
                services.AddSingleton<OAuthProviderService>();
                services.AddSingleton<GoogleDriveService>();
                services.AddSingleton<GoogleDriveTokenHolder>();
            });

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Save last window settings
        }
    }
}
