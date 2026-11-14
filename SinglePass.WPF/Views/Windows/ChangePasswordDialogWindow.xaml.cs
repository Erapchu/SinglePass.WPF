using SinglePass.WPF.Localization;
using SinglePass.WPF.Services;
using System.ComponentModel;
using System.Windows;

namespace SinglePass.WPF.Views.Windows
{
    public partial class ChangePasswordDialogWindow : Window
    {
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private bool _isSaving;

        internal ChangePasswordDialogWindow(CredentialsCryptoService credentialsCryptoService)
        {
            _credentialsCryptoService = credentialsCryptoService;
            InitializeComponent();
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
            _isSaving = true;
            PasswordFields.IsEnabled = SaveButton.IsEnabled = CancelButton.IsEnabled = false;
            var result = await _credentialsCryptoService.ChangePasswordAsync(CurrentPasswordBox.Password, newPassword);
            _isSaving = false;

            if (result == PasswordChangeResult.Changed)
            {
                DialogResult = true;
                return;
            }

            PasswordFields.IsEnabled = SaveButton.IsEnabled = CancelButton.IsEnabled = true;
            if (result == PasswordChangeResult.IncorrectCurrentPassword)
            {
                CurrentPasswordBox.Focus();
                CurrentPasswordBox.SelectAll();
                ShowError("CurrentPasswordIsIncorrect");
                return;
            }

            ShowError("PasswordChangeFailed");
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (_isSaving)
                e.Cancel = true;
        }

        private void ShowError(string key)
        {
            ErrorText.Text = LocalizationManager.Instance[key];
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
