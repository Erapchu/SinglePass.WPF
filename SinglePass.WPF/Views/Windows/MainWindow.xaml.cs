using SinglePass.WPF.Helpers;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Controls;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly AppSettingsService _appSettingsService;
        private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(3) };

        private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        public MainWindow(
            MainWindowViewModel mainViewModel,
            AppSettingsService appSettingsService)
        {
            InitializeComponent();

            _appSettingsService = appSettingsService;

            var windowSettings = _appSettingsService.MainWindowSettings;
            if (windowSettings is not null)
            {
                var windowRect = new Rect(windowSettings.Left, windowSettings.Top, windowSettings.Width, windowSettings.Height);
                if (WindowPositionHelper.CheckIsOnAnyScreen(windowRect))
                {
                    Left = windowSettings.Left;
                    Top = windowSettings.Top;
                    Width = windowSettings.Width;
                    Height = windowSettings.Height;
                }
            }

            mainViewModel.CredentialSelected += Vm_CredentialSelected;
            mainViewModel.ScrollIntoViewRequired += MainViewModel_ScrollIntoViewRequired;
            mainViewModel.PasswordsVM.StatusMessageRequested += ShowStatusMessage;
            _statusTimer.Tick += StatusTimer_Tick;

            DataContext = mainViewModel;
        }

        private void MainViewModel_ScrollIntoViewRequired(CredentialViewModel credVM)
        {
            if (ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
                passwordsControl.CredentialsListBox.ScrollIntoView(credVM);
        }

        private void ShowStatusMessage(string message)
        {
            StatusText.Text = message;
            StatusBanner.Visibility = Visibility.Visible;
            _statusTimer.Stop();
            _statusTimer.Start();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            _statusTimer.Stop();
            StatusBanner.Visibility = Visibility.Collapsed;
        }

        private void Vm_CredentialSelected(CredentialViewModel credVM)
        {
            var passStringLength = credVM?.PasswordFieldVM?.Value?.Length ?? 0;
            if (ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
            {
                passwordsControl.CredentialsDetailsControl.PasswordFieldBox.Password = new string('*', passStringLength);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //TODO: Shutdown process on main window close or hide window and cancel depend on settings
            Hide();
            ShowInTaskbar = false;
            e.Cancel = true;
        }

        private void ListBoxItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var anyCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (anyCtrlPressed)
                e.Handled = true;
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visibility && visibility && ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
            {
                Application.Current.Dispatcher.InvokeAsync(() => passwordsControl.SearchTextBox.Focus());
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            _statusTimer.Stop();
            ViewModel.PasswordsVM.StatusMessageRequested -= ShowStatusMessage;

            var saveRequired = false;
            if (ViewModel.SettingsVM.ThemeMode != _appSettingsService.ThemeMode)
            {
                _appSettingsService.ThemeMode = ViewModel.SettingsVM.ThemeMode;
                saveRequired = true;
            }

            if (!ViewModel.SettingsVM.ShowPopupHotkey.Equals(_appSettingsService.ShowPopupHotkey))
            {
                _appSettingsService.ShowPopupHotkey = ViewModel.SettingsVM.ShowPopupHotkey;
                saveRequired = true;
            }

            if (ViewModel.PasswordsVM.Sort != _appSettingsService.Sort)
            {
                _appSettingsService.Sort = ViewModel.PasswordsVM.Sort;
                saveRequired = true;
            }

            if (ViewModel.PasswordsVM.Order != _appSettingsService.Order)
            {
                _appSettingsService.Order = ViewModel.PasswordsVM.Order;
                saveRequired = true;
            }

            // Avoid minimized state
            if (WindowState != WindowState.Minimized)
            {
                var currentWindowSettings = new WindowSettings()
                {
                    Left = Left,
                    Top = Top,
                    Width = Width,
                    Height = Height,
                    WindowState = WindowState
                };
                if (!currentWindowSettings.Equals(_appSettingsService.MainWindowSettings))
                {
                    _appSettingsService.MainWindowSettings = currentWindowSettings;
                    saveRequired = true;
                }
            }

            if (saveRequired)
            {
                // Save settings and wait to avoid file corruptions
                _appSettingsService.Save().Wait();
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = _appSettingsService.MainWindowSettings?.WindowState ?? WindowState.Normal;
        }
    }
}
