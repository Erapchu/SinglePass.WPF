using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
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
        private IHost _host;
        private ILogger<App> _logger;
        private static IConfiguration _configuration;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogError(e.Exception, "Dispatcher unhandled exception");
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.LogError(e.ExceptionObject as Exception, "Domain unhandled exception");
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeComponent();

            _configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("settings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            _host = CreateHostBuilder().Build();
            _logger = _host.Services.GetService<ILogger<App>>();
            _logger.LogInformation("Log session started!");

            var mainWindow = _host.Services.GetService<MainWindow>();
            mainWindow.Show();
        }

        private IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder().ConfigureServices(services =>
            {
                // NLog
                services.AddLogging(lb =>
                {
                    lb.ClearProviders();
                    lb.SetMinimumLevel(LogLevel.Trace);
                    lb.AddNLog(_configuration);
                });

                services.AddScoped<MainWindow>();
                services.AddScoped<MainWindowViewModel>();
                services.AddScoped<SettingsService>();
                services.AddScoped<PasswordsViewModel>();
                services.AddScoped<SettingsViewModel>();
                services.AddScoped<CredentialsDialogViewModel>();

                services.AddSingleton<ThemeService>();
            });

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Save last window settings
        }
    }
}
