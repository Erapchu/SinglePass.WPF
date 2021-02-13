using Autofac;
using NLog;
using PasswordManager.Helpers;
using PasswordManager.ViewModels;
using PasswordManager.Views;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PasswordManager
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IContainer _container;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            InitializeComponent();
            ConfigureServices();

            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices()
        {
            var container = new ContainerBuilder();
            container.RegisterType<MainWindow>();
            container.RegisterType<MainWindowViewModel>();
            container.Register(LoggerResolver.GetLogger).SingleInstance();

            _container = container.Build();
        }
    }
}
