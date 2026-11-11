using SinglePass.WPF.Helpers;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Controls;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Let Fluent resolve the system theme and notify the workspace when its colors change.
        private static readonly DependencyProperty FluentTextBrushProperty = DependencyProperty.Register(
            "FluentTextBrush",
            typeof(Brush),
            typeof(MainWindow),
            new PropertyMetadata(null, (window, _) => ((MainWindow)window).UpdateWorkspacePalette()));

        private readonly AppSettingsService _appSettingsService;
        private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(3) };
        private ResourceDictionary _workspacePalette;

        private MainWindowViewModel ViewModel => DataContext as MainWindowViewModel;

        public MainWindow(
            MainWindowViewModel mainViewModel,
            AppSettingsService appSettingsService)
        {
            InitializeComponent();

            SetResourceReference(FluentTextBrushProperty, "TextFillColorPrimaryBrush");
            UpdateWorkspacePalette();
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;

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

            mainViewModel.ScrollIntoViewRequired += MainViewModel_ScrollIntoViewRequired;
            mainViewModel.PasswordsVM.StatusMessageRequested += ShowStatusMessage;
            _statusTimer.Tick += StatusTimer_Tick;

            DataContext = mainViewModel;
        }

        private void UpdateWorkspacePalette()
        {
            var theme = SystemParameters.HighContrast ? "Contrast"
                : GetValue(FluentTextBrushProperty) is SolidColorBrush { Color.ScG: > 0.5f } ? "Dark" : "Light";
            var source = new Uri($"/SinglePass.WPF;component/Themes/Workspace.{theme}.xaml", UriKind.Relative);
            if (_workspacePalette?.Source == source)
                return;

            var palette = new ResourceDictionary { Source = source };
            var dictionaries = Resources.MergedDictionaries;
            if (_workspacePalette is null)
                dictionaries.Add(palette);
            else
                dictionaries[dictionaries.IndexOf(_workspacePalette)] = palette;

            _workspacePalette = palette;
        }

        private void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
                UpdateWorkspacePalette();
        }

        private void MinimizeWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

        private void ToggleMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e) => SystemCommands.CloseWindow(this);

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
            SystemParameters.StaticPropertyChanged -= SystemParameters_StaticPropertyChanged;
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
