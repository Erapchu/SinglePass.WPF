﻿using Autofac;
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
        private IContainer _container;
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
            ConfigureServices();
            _ = Task.Run(() =>
            {
                _logger = _container.Resolve<ILogger>();
                _logger.Info("Log session started!");
            });

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
