using Erapkey.Core.Localization;
using Erapkey.Desktop.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using Erapkey.Core.Services;

namespace Erapkey.Desktop.Views.Controls
{
    /// <summary>
    /// Changes the master password. Shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class ChangePasswordDialog : UserControl, IModalDialog
    {
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private bool _isSaving;

        internal ChangePasswordDialog(CredentialsCryptoService credentialsCryptoService)
        {
            _credentialsCryptoService = credentialsCryptoService;
            InitializeComponent();
        }

        /// <summary>Whether the new password has been applied.</summary>
        public bool Changed { get; private set; }

        public event Action CloseRequested;

        FrameworkElement IModalDialog.Footer => Footer;

        public IInputElement InitialFocus => CurrentPasswordBox;

        public void Cancel()
        {
            if (!_isSaving)
                CloseRequested?.Invoke();
        }

        private void PasswordInput_Changed(object sender, RoutedEventArgs e)
        {
            using var current = CurrentPasswordBox.SecurePassword;
            using var next = NewPasswordBox.SecurePassword;
            ErrorText.Visibility = Visibility.Collapsed;
            SaveButton.IsEnabled = current.Length > 0 && next.Length > 0;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            var newPassword = NewPasswordBox.Password;
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                ShowError("Minimum8Characters");
                return;
            }

            ErrorText.Visibility = Visibility.Collapsed;
            SetSaving(true);
            var result = await _credentialsCryptoService.ChangePasswordAsync(CurrentPasswordBox.Password, newPassword);
            SetSaving(false);

            if (result == PasswordChangeResult.Changed)
            {
                Changed = true;
                CloseRequested?.Invoke();
                return;
            }

            if (result == PasswordChangeResult.IncorrectCurrentPassword)
            {
                CurrentPasswordBox.Focus();
                CurrentPasswordBox.SelectAll();
                ShowError("CurrentPasswordIsIncorrect");
                return;
            }

            SaveButton.Focus();
            ShowError("PasswordChangeFailed");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Cancel();

        private void SetSaving(bool saving)
        {
            _isSaving = saving;
            PasswordFields.IsEnabled = SaveButton.IsEnabled = CancelButton.IsEnabled = !saving;
        }

        private void ShowError(string key)
        {
            ErrorText.Text = LocalizationManager.Instance[key];
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
