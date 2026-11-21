using SinglePass.Core.Models;
using SinglePass.WPF.Services;
using SinglePass.Core.Localization;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.Views.Windows;
using System;
using System.Text;
using System.IO;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SinglePass.Core.Services;

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

        private async void DeleteButton_Click(object sender, RoutedEventArgs e) => await DeleteCredentialAsync();

        internal async Task DeleteCredentialAsync()
        {
            if (DataContext is not PasswordsViewModel viewModel || !viewModel.DeleteCommand.CanExecute(null))
                return;

            if (Window.GetWindow(this) is MainWindow window && Parent is Decorator anchor)
                window.ShowCredentialDeletion(this, anchor, DeleteButton, viewModel);
            else
                await viewModel.DeleteCommand.ExecuteAsync(null);
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

        internal void UseDefaultIcon() => CredentialIcon.SetResourceReference(Image.SourceProperty, "DefaultFavIcon");

        private void CredentialIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Image { IsVisible: true } icon)
                icon.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
        }

        private void PasswordHistory_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            ((Expander)sender).IsExpanded = false;

        private void PasswordHistory_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Expander { IsVisible: false } history)
                history.IsExpanded = false;
        }

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

            var viewing = UpdateTotpDisplay(TotpDisplay, TotpCode, TotpCountdown, TotpProgressArc);
            var editing = UpdateTotpDisplay(EditTotpDisplay, EditTotpCode, EditTotpCountdown, EditTotpProgressArc);
            CopyTotpButton.IsEnabled = viewing;
            _totpTimer.IsEnabled = viewing || editing;
        }

        private static bool UpdateTotpDisplay(StackPanel display, TextBlock codeText, Grid countdown,
            System.Windows.Shapes.Path arc)
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

        #region SSH key
        // Real key files are a few kilobytes; anything much larger is not a key.
        private const long MaxSshKeyFileSize = 64 * 1024;

        private static string SshKeyFileFilter =>
            $"{LocalizationManager.Instance["SshKeyFiles"]}|id_*;*.pem;*.key;*.pub|{LocalizationManager.Instance["AllFiles"]}|*.*";

        private static string SshDirectory
        {
            get
            {
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
                return Directory.Exists(path) ? path : null;
            }
        }

        private void PrivateKeyEditBox_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            SshKeyStatus?.SetCurrentValue(TextBlock.TextProperty, string.Empty);

        private void ShowSshKeyStatus(string resourceKey) =>
            SshKeyStatus.Text = resourceKey == null ? string.Empty : LocalizationManager.Instance[resourceKey];

        private void ImportSshKey_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayedDraft is not { IsSshKey: true } draft)
                return;

            var dialog = new OpenFileDialog { Filter = SshKeyFileFilter, InitialDirectory = SshDirectory };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true || !ReferenceEquals(DisplayedDraft, draft))
                return;

            try
            {
                var path = dialog.FileName;
                var isPublicFile = path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase);
                var keyPath = isPublicFile ? path[..^4] : path;
                var text = ReadKeyFile(path);
                string privateKey = null, publicKey = null;
                if (SshKeys.TryParsePublicKey(text, out _, out _, out _))
                {
                    // A public key was picked: take the private key from the file next to it, if there is one.
                    publicKey = text.Trim();
                    if (isPublicFile && File.Exists(keyPath))
                        privateKey = ReadKeyFile(keyPath);
                }
                else if (text.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
                {
                    privateKey = text;
                    var publicPath = path + ".pub";
                    if (File.Exists(publicPath) && ReadKeyFile(publicPath) is var publicText &&
                        SshKeys.TryParsePublicKey(publicText, out _, out _, out _))
                        publicKey = publicText.Trim();
                }
                else
                {
                    ShowSshKeyStatus("InvalidSshKeyFile");
                    return;
                }

                // The public key goes last: setting the private key may replace it with one without a comment.
                if (privateKey != null)
                    draft.SshPrivateKey = SshKeys.NormalizePrivateKey(privateKey);
                if (publicKey != null)
                    draft.SshPublicKey = publicKey;
                if (!draft.NameFieldVM.HasText)
                    draft.NameFieldVM.Value = Path.GetFileName(keyPath);
                ShowSshKeyStatus(null);
            }
            catch (InvalidDataException)
            {
                ShowSshKeyStatus("SshKeyFileTooLarge");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowSshKeyStatus("InvalidSshKeyFile");
            }
        }

        private static string ReadKeyFile(string path)
        {
            if (new FileInfo(path).Length > MaxSshKeyFileSize)
                throw new InvalidDataException("The file is too large to be an SSH key.");

            return File.ReadAllText(path);
        }

        private void GenerateSshKey_Click(object sender, RoutedEventArgs e)
        {
            if (DisplayedDraft is not { IsSshKey: true, HasSshPrivateKey: false } draft)
                return;

            // Same default comment as ssh-keygen.
            var (privateKey, publicKey) = SshKeys.GenerateEd25519($"{Environment.UserName}@{Environment.MachineName}");
            draft.SshPrivateKey = privateKey;
            draft.SshPublicKey = publicKey;
            draft.SshPassphrase = null;
            ShowSshKeyStatus(null);
        }

        private void SavePrivateKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not PasswordsViewModel viewModel ||
                viewModel.SelectedCredentialVM is not { IsSshKey: true, HasSshPrivateKey: true } credential)
                return;

            var keyType = credential.SshKeyInfo?.KeyType;
            var fileName = keyType switch
            {
                "ssh-ed25519" => "id_ed25519",
                "ssh-rsa" => "id_rsa",
                _ when keyType?.StartsWith("ecdsa-", StringComparison.Ordinal) == true => "id_ecdsa",
                _ => "id_key"
            };
            var dialog = new SaveFileDialog
            {
                Filter = SshKeyFileFilter,
                FileName = fileName,
                InitialDirectory = SshDirectory,
                AddExtension = false,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            try
            {
                // OpenSSH rejects keys with CRLF line endings or without a final newline.
                File.WriteAllText(dialog.FileName, SshKeys.NormalizePrivateKey(credential.SshPrivateKey), new UTF8Encoding(false));
                viewModel.ShowStatus(LocalizationManager.Instance["PrivateKeySaved"]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, LocalizationManager.Instance["SavePrivateKey"],
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

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
