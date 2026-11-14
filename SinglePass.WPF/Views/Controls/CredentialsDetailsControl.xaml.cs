using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for CredentialsDetailsControl.xaml
    /// </summary>
    public partial class CredentialsDetailsControl : UserControl
    {
        private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };

        public CredentialsDetailsControl()
        {
            _totpTimer.Tick += (_, _) => RefreshTotp();
            InitializeComponent();
        }

        private void TotpDisplay_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshTotp();
        private void TotpDisplay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshTotp();
        private void TotpDisplay_Loaded(object sender, RoutedEventArgs e) => RefreshTotp();

        private void TotpDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            _totpTimer.Stop();
            TotpCode.Clear();
            TotpCountdown.Text = string.Empty;
        }

        private void RefreshTotp()
        {
            if (TotpCode == null || TotpCountdown == null || CopyTotpButton == null)
                return;

            var configuration = TotpDisplay.IsLoaded && TotpDisplay.IsVisible ? TotpDisplay.DataContext as TotpConfiguration : null;
            var valid = TotpService.TryGetCode(configuration, DateTime.UtcNow, out var code, out var seconds);
            TotpCode.Text = code ?? string.Empty;
            TotpCountdown.Text = valid ? $"{seconds / 60}:{seconds % 60:00}" : string.Empty;
            CopyTotpButton.IsEnabled = valid;
            _totpTimer.IsEnabled = valid;
        }

        private void TotpSetupBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            ((PasswordBox)sender).Clear();

        private void TotpSetupBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var box = (PasswordBox)sender;
            if (box.DataContext is CredentialViewModel draft)
                draft.TotpSetupInput = box.Password;
        }

        private void NameEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (!NameEditBox.IsVisible)
                    return;

                EditScrollViewer.ScrollToTop();
                NameEditBox.Focus();
            }, DispatcherPriority.Input);
        }
    }
}
