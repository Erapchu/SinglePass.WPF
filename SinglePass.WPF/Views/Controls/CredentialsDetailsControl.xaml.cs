using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.Localization;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Windows;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for CredentialsDetailsControl.xaml
    /// </summary>
    public partial class CredentialsDetailsControl : UserControl
    {
        public static readonly DependencyProperty DisplayedDraftProperty = DependencyProperty.Register(
            nameof(DisplayedDraft), typeof(CredentialViewModel), typeof(CredentialsDetailsControl),
            new PropertyMetadata(null, (sender, e) => ((CredentialsDetailsControl)sender).SetValue(IsEditingPropertyKey, e.NewValue != null)));

        private static readonly DependencyPropertyKey IsEditingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsEditing), typeof(bool), typeof(CredentialsDetailsControl), new PropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty = IsEditingPropertyKey.DependencyProperty;

        public CredentialViewModel DisplayedDraft
        {
            get => (CredentialViewModel)GetValue(DisplayedDraftProperty);
            set => SetValue(DisplayedDraftProperty, value);
        }

        public bool IsEditing => (bool)GetValue(IsEditingProperty);

        private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private bool _readingTotpQr;

        public CredentialsDetailsControl()
        {
            _totpTimer.Tick += (_, _) => RefreshTotp();
            InitializeComponent();
            FollowDraft();
        }

        internal void FollowDraft() => SetBinding(DisplayedDraftProperty, new Binding(nameof(PasswordsViewModel.DraftCredential)));

        internal double MeasureEditorHeight(double width)
        {
            Measure(new Size(width, double.PositiveInfinity));
            return DesiredSize.Height;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e) => EditCredential();

        internal void EditCredential()
        {
            if (DataContext is not PasswordsViewModel viewModel || !viewModel.EditCommand.CanExecute(null))
                return;

            if (Window.GetWindow(this) is MainWindow window && Parent is Decorator anchor)
                window.ShowCredentialEditor(this, anchor, EditButton, viewModel);
            else
                viewModel.EditCommand.Execute(null);
        }

        internal void UseModalSurface(bool enabled)
        {
            if (enabled)
            {
                CardBorder.Background = Brushes.Transparent;
                CardBorder.BorderBrush = Brushes.Transparent;
            }
            else
            {
                CardBorder.ClearValue(Border.BackgroundProperty);
                CardBorder.ClearValue(Border.BorderBrushProperty);
            }
        }

        internal void AnimateEditActions(AnimationTimeline animation)
        {
            TotpEditActions.BeginAnimation(OpacityProperty, animation);
            TotpSetupFields.BeginAnimation(OpacityProperty, animation);
            EditActions.BeginAnimation(OpacityProperty, animation);
        }

        internal void UseDefaultIcon() => CredentialIcon.SetResourceReference(Image.SourceProperty, "DefaultFavIcon");

        private void TotpDisplay_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshTotp();
        private void TotpDisplay_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => RefreshTotp();
        private void TotpDisplay_Loaded(object sender, RoutedEventArgs e) => RefreshTotp();

        private void TotpDisplay_Unloaded(object sender, RoutedEventArgs e)
        {
            _totpTimer.Stop();
            TotpCode.Text = string.Empty;
            TotpProgressArc.Data = null;
            EditTotpCode.Text = string.Empty;
            EditTotpProgressArc.Data = null;
        }

        private void RefreshTotp()
        {
            if (TotpProgressArc == null || EditTotpProgressArc == null)
                return;

            var viewing = UpdateTotpDisplay(TotpDisplay, TotpCode, TotpCountdown, TotpProgressArc, CopyTotpButton);
            var editing = UpdateTotpDisplay(EditTotpDisplay, EditTotpCode, EditTotpCountdown, EditTotpProgressArc, EditCopyTotpButton);
            _totpTimer.IsEnabled = viewing || editing;
        }

        private static bool UpdateTotpDisplay(StackPanel display, TextBlock codeText, Grid countdown,
            System.Windows.Shapes.Path arc, Button copyButton)
        {
            var configuration = display.IsLoaded && display.IsVisible ? display.DataContext as TotpConfiguration : null;
            var now = DateTime.UtcNow;
            var valid = TotpService.TryGetCode(configuration, now, out var code, out var seconds);
            codeText.Text = valid ? code.Insert(code.Length / 2, " ") : string.Empty;
            countdown.Visibility = valid ? Visibility.Visible : Visibility.Collapsed;
            arc.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,
                valid && seconds <= 5 ? "SystemFillColorCriticalBrush" : "AccentFillColorDefaultBrush");
            arc.Data = valid
                ? CreateProgressArc((seconds - now.Millisecond / 1000.0) / configuration.PeriodSeconds)
                : null;
            copyButton.IsEnabled = valid;
            return valid;
        }

        private void ReplaceTotp_Click(object sender, RoutedEventArgs e)
        {
            if (TotpSetupBox.DataContext is CredentialViewModel draft)
            {
                draft.ReplaceTotp = true;
                TotpSetupBox.Focus();
            }
        }

        private void RemoveTotp_Click(object sender, RoutedEventArgs e)
        {
            if (TotpSetupBox.DataContext is CredentialViewModel draft)
                draft.RemoveTotp = true;
        }

        private void CancelTotpChange_Click(object sender, RoutedEventArgs e)
        {
            if (TotpSetupBox.DataContext is CredentialViewModel draft)
            {
                draft.ReplaceTotp = false;
                draft.RemoveTotp = false;
                draft.HasInvalidTotp = false;
                TotpSetupBox.Clear();
                TotpQrStatus.Text = string.Empty;
            }
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
