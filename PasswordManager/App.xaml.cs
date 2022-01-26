using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog;
using PasswordManager.Services;
using PasswordManager.ViewModels;
using PasswordManager.Views;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace PasswordManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IHost _host;
        private ILogger _logger;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            _logger?.Error(e.ExceptionObject as Exception);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeComponent();

            var hostBuilder = CreateHostBuilder();
            _host = hostBuilder.Build();
            _logger = _host.Services.GetService<ILogger>();
            _logger.Info("Log session started!");

            var mainWindow = _host.Services.GetService<MainWindow>();
            mainWindow.Show();
        }

        private IHostBuilder CreateHostBuilder() =>
            Host.CreateDefaultBuilder().ConfigureServices(services =>
            {
                services.AddScoped<MainWindow>();
                services.AddScoped<MainWindowViewModel>();
                services.AddScoped<SettingsService>();
                services.AddScoped<PasswordsViewModel>();
                services.AddScoped<SettingsViewModel>();
                services.AddScoped<CredentialsDialogViewModel>();

                services.AddSingleton<ThemeService>();
                services.AddSingleton(s => LoggerResolver.GetLogger());
            });

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            // Save last window settings
        }
    }
}
