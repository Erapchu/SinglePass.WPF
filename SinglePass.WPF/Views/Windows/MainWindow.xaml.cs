using SinglePass.WPF.Helpers;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Controls;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private CloudSyncControl _cloudSyncControl;

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

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.OriginalSource != sender || e.AddedItems.Count == 0)
                return;

            if (NavigationContent.Content is CloudSyncControl)
                CloseCloudSync();

            SettingsHeader.Visibility = e.AddedItems[0] is NavigationItemViewModel { Content: SettingsControl }
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (e.RemovedItems.Count == 0 || !IsVisible || !SystemParameters.ClientAreaAnimation)
                return;

            var duration = TimeSpan.FromMilliseconds(160);
            AnimateNavigation(new Vector(0, 8), 0.85, duration);
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var navigation = (ListView)sender;
            if (navigation.ItemContainerGenerator.ContainerFromItem(e.AddedItems[0]) is ListViewItem item)
            {
                var scale = new ScaleTransform();
                item.RenderTransformOrigin = new Point(0.5, 0.5);
                item.RenderTransform = scale;
                var selectionAnimation = new DoubleAnimation(0.97, 1, duration)
                {
                    EasingFunction = easing,
                    FillBehavior = FillBehavior.Stop
                };
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, selectionAnimation);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, selectionAnimation);
            }
        }

        private void OpenCloudSync_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (NavigationContent.Content is not SettingsControl)
                return;

            _cloudSyncControl ??= new CloudSyncControl { DataContext = ViewModel.CloudSyncVM };
            NavigationContent.Content = _cloudSyncControl;
            CloudSyncBreadcrumb.Visibility = Visibility.Visible;
            AnimateSettingsPage(64);
            Dispatcher.InvokeAsync(() => SettingsBreadcrumbButton.Focus(), DispatcherPriority.Loaded);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (e.ChangedButton == MouseButton.XButton1 && NavigationContent.Content is CloudSyncControl)
            {
                e.Handled = true;
                ReturnToSettings();
            }
        }

        private void SettingsBreadcrumbButton_Click(object sender, RoutedEventArgs e) => ReturnToSettings();

        private void ReturnToSettings()
        {
            CloseCloudSync();
            AnimateSettingsPage(-64);

            if (NavigationContent.Content is SettingsControl settings)
                Dispatcher.InvokeAsync(() => settings.CloudSyncButton.Focus(), DispatcherPriority.Loaded);
        }

        private void CloseCloudSync()
        {
            // Clearing the local value restores the binding to the selected sidebar item.
            NavigationContent.ClearValue(ContentControl.ContentProperty);
            CloudSyncBreadcrumb.Visibility = Visibility.Collapsed;
        }

        private void AnimateSettingsPage(double horizontalOffset)
        {
            if (IsVisible && SystemParameters.ClientAreaAnimation)
                AnimateNavigation(new Vector(horizontalOffset, 0), 0, TimeSpan.FromMilliseconds(250));
        }

        private void AnimateNavigation(Vector offset, double initialOpacity, TimeSpan duration)
        {
            var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            DoubleAnimation CreateAnimation(double from, double to) => new(from, to, duration)
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.Stop
            };

            NavigationContent.BeginAnimation(OpacityProperty, CreateAnimation(initialOpacity, 1));
            NavigationOffset.BeginAnimation(TranslateTransform.XProperty, offset.X == 0 ? null : CreateAnimation(offset.X, 0));
            NavigationOffset.BeginAnimation(TranslateTransform.YProperty, offset.Y == 0 ? null : CreateAnimation(offset.Y, 0));
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
            if (ViewModel.SettingsVM.Language != _appSettingsService.Language)
            {
                _appSettingsService.Language = ViewModel.SettingsVM.Language;
                saveRequired = true;
            }

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

            var detailsPaneRatio = ViewModel.PasswordsVM.DetailsPaneRatio;
            if (detailsPaneRatio != _appSettingsService.DetailsPaneRatio)
            {
                _appSettingsService.DetailsPaneRatio = detailsPaneRatio;
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
