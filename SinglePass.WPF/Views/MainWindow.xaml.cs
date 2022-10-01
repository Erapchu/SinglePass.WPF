using SinglePass.WPF.Controls;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Controls;
using System;
using System.Windows;
using System.Windows.Input;

namespace SinglePass.WPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MaterialWindow
    {
        private readonly HotkeysService _hotkeyService;
        private readonly AppSettingsService _appSettingsService;

        private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        public MainWindow(
            MainWindowViewModel mainViewModel,
            HotkeysService hotkeysService,
            AppSettingsService appSettingsService)
        {
            InitializeComponent();

            _hotkeyService = hotkeysService;
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
                    WindowState = windowSettings.WindowState;
                }
            }

            mainViewModel.CredentialSelected += Vm_CredentialSelected;
            mainViewModel.ScrollIntoViewRequired += MainViewModel_ScrollIntoViewRequired;
            mainViewModel.PasswordsVM.ActiveCredentialDialogVM.EnqueueSnackbarMessage += ActiveCredentialDialogVM_EnqueueSnackbarMessage;

            DataContext = mainViewModel;
        }

        private void MainViewModel_ScrollIntoViewRequired(CredentialViewModel credVM)
        {
            if (ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
                passwordsControl.CredentialsListBox.ScrollIntoView(credVM);
        }

        private void ActiveCredentialDialogVM_EnqueueSnackbarMessage(string message)
        {
            if (SnackbarMain.MessageQueue is { } messageQueue)
            {
                messageQueue.Enqueue(message);
            }
        }

        private void Vm_CredentialSelected(CredentialViewModel credVM)
        {
            var passStringLength = credVM?.PasswordFieldVM?.Value?.Length ?? 0;
            if (ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
            {
                passwordsControl.CredentialsDetailsControl.PasswordFieldBox.Password = new string('*', passStringLength);
            }
        }

        private void MaterialWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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

        private void MaterialWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool visibility && visibility && ViewModel.SelectedNavigationItem?.Content is PasswordsControl passwordsControl)
            {
                Application.Current.Dispatcher.InvokeAsync(() => passwordsControl.SearchTextBox.Focus());
            }
        }

        private void MaterialWindow_Closed(object sender, EventArgs e)
        {
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
    }
}
