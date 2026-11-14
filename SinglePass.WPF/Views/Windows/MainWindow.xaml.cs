using SinglePass.WPF.Helpers;
using SinglePass.WPF.Localization;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Controls;
using SinglePass.WPF.Views.Helpers;
using System;
using System.ComponentModel;
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
            mainViewModel.PropertyChanged += ViewModel_PropertyChanged;
            _statusTimer.Tick += StatusTimer_Tick;

            DataContext = mainViewModel;
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainWindowViewModel.SelectedNavigationItem))
                return;

            if (CloudSyncBreadcrumb.Visibility == Visibility.Visible)
                CloseCloudSync();

            var showSettings = ViewModel.SelectedNavigationItem == ViewModel.SettingsNavigation;
            var pageChanged = showSettings != (SettingsHeader.Visibility == Visibility.Visible);
            SettingsHeader.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
            PasswordsHeading.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;

            if (pageChanged && IsVisible && SystemParameters.ClientAreaAnimation)
                AnimateNavigation(new Vector(0, 8), 0.85, TimeSpan.FromMilliseconds(160));
        }

        private async void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PasswordsVM.IsSavingGroups)
                return;

            var dialog = new PasswordGroupDialogWindow { Owner = this };
            if (dialog.ShowDialog() == true)
                await ViewModel.AddGroupAsync(dialog.Group);
        }

        private void GroupMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }

        private async void EditGroup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PasswordsVM.IsSavingGroups ||
                sender is not MenuItem { DataContext: NavigationItemViewModel { Group: { } group } item })
                return;

            var dialog = new PasswordGroupDialogWindow(group) { Owner = this };
            if (dialog.ShowDialog() == true)
                await ViewModel.EditGroupAsync(item, dialog.Group);
        }

        private async void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.PasswordsVM.IsSavingGroups ||
                sender is not FrameworkElement { DataContext: NavigationItemViewModel { Group: { } group } item })
                return;

            var loc = LocalizationManager.Instance;
            var choice = MessageDialog.ShowDialog(loc["DeletePasswordGroup"],
                string.Format(loc["DeletePasswordGroupConfirmation"], group.Name), MessageDialogButtons.YesNo, "\uE74D");
            if (choice == DialogChoice.Yes)
                await ViewModel.DeleteGroupAsync(item);
        }

        private void Group_DragOver(object sender, DragEventArgs e)
        {
            var row = (Border)sender;
            var canMove = TryGetGroupDrop(row, e, out _, out _);
            e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
            if (canMove)
                row.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
            else
                row.ClearValue(Border.BackgroundProperty);
            e.Handled = true;
        }

        private void Group_DragLeave(object sender, DragEventArgs e) => ((Border)sender).ClearValue(Border.BackgroundProperty);

        private async void Group_Drop(object sender, DragEventArgs e)
        {
            var row = (Border)sender;
            row.ClearValue(Border.BackgroundProperty);
            e.Handled = true;
            var canMove = TryGetGroupDrop(row, e, out var credentialIds, out var groupId);
            e.Effects = canMove ? DragDropEffects.Move : DragDropEffects.None;
            if (canMove)
                await ViewModel.PasswordsVM.MoveToGroupAsync(credentialIds, groupId);
        }

        private bool TryGetGroupDrop(Border row, DragEventArgs e, out Guid[] credentialIds, out Guid? groupId)
        {
            credentialIds = e.Data.GetData(PasswordsControl.CredentialDragFormat, false) as Guid[];
            groupId = (row.DataContext as NavigationItemViewModel)?.Group?.Id;
            return e.AllowedEffects.HasFlag(DragDropEffects.Move) &&
                credentialIds != null && ViewModel.PasswordsVM.CanMoveToGroup(credentialIds, groupId);
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

        private bool _closingForVaultLock;

        internal void CloseForVaultLock()
        {
            _closingForVaultLock = true;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_closingForVaultLock)
                return;

            //TODO: Shutdown process on main window close or hide window and cancel depend on settings
            Hide();
            ShowInTaskbar = false;
            e.Cancel = true;
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
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
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
