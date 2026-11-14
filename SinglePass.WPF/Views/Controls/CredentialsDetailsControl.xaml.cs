using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.Localization;
using SinglePass.WPF.ViewModels;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for CredentialsDetailsControl.xaml
    /// </summary>
    public partial class CredentialsDetailsControl : UserControl
    {
        private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private bool _readingTotpQr;

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
            TotpCode.Text = string.Empty;
            TotpProgressArc.Data = null;
        }

        private void RefreshTotp()
        {
            if (TotpCode == null || TotpProgressArc == null || CopyTotpButton == null)
                return;

            var configuration = TotpDisplay.IsLoaded && TotpDisplay.IsVisible ? TotpDisplay.DataContext as TotpConfiguration : null;
            var now = DateTime.UtcNow;
            var valid = TotpService.TryGetCode(configuration, now, out var code, out var seconds);
            TotpCode.Text = valid ? code.Insert(code.Length / 2, " ") : string.Empty;
            TotpCountdown.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
            TotpProgressArc.Data = valid
                ? CreateProgressArc((seconds - now.Millisecond / 1000.0) / configuration.PeriodSeconds)
                : null;
            CopyTotpButton.IsEnabled = valid;
            _totpTimer.IsEnabled = valid;
        }

        private static Geometry CreateProgressArc(double fraction)
        {
            const double center = 8;
            const double radius = 7;
            if (fraction >= 1)
                return new EllipseGeometry(new Point(center, center), radius, radius);

            var angle = (1 - fraction) * 2 * Math.PI;
            var start = new Point(center + radius * Math.Sin(angle), center - radius * Math.Cos(angle));
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(start, isFilled: false, isClosed: false);
                context.ArcTo(new Point(center, center - radius), new Size(radius, radius), 0, fraction > 0.5,
                    SweepDirection.Clockwise, isStroked: true, isSmoothJoin: false);
            }
            geometry.Freeze();
            return geometry;
        }

        private void TotpSetupBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            ((PasswordBox)sender).Clear();
            TotpQrStatus?.SetCurrentValue(TextBlock.TextProperty, string.Empty);
        }

        private void TotpSetupBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            var box = (PasswordBox)sender;
            TotpQrStatus?.SetCurrentValue(TextBlock.TextProperty, string.Empty);
            if (box.DataContext is CredentialViewModel draft)
                draft.TotpSetupInput = box.Password;
        }

        private async void PasteTotpQr_Click(object sender, RoutedEventArgs e)
        {
            if (_readingTotpQr || TotpSetupBox.DataContext is not CredentialViewModel draft || draft.RemoveTotp)
                return;

            var previousInput = draft.TotpSetupInput;
            _readingTotpQr = true;
            try
            {
                var image = Clipboard.GetImage();
                if (image == null)
                {
                    ShowQrStatus("NoTotpQrImage");
                    return;
                }

                image.Freeze();
                var link = await Task.Run(() => TotpQrReader.ReadSetupLink(image));
                if (!TotpSetupBox.IsVisible || !TotpSetupBox.IsEnabled ||
                    !ReferenceEquals(TotpSetupBox.DataContext, draft) || draft.TotpSetupInput != previousInput)
                    return;

                if (link != null)
                    TotpSetupBox.Password = link;
                ShowQrStatus(link != null ? "TotpQrImported" : "InvalidTotpQr");
            }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                if (TotpSetupBox.IsVisible && ReferenceEquals(TotpSetupBox.DataContext, draft))
                    ShowQrStatus("InvalidTotpQr");
            }
            finally
            {
                _readingTotpQr = false;
            }
        }

        private void ShowQrStatus(string resourceKey) => TotpQrStatus.Text = LocalizationManager.Instance[resourceKey];

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
