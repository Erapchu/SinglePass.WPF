using Microsoft.Win32;
using Erapkey.Core.Localization;
using Erapkey.Core.Services;
using Erapkey.ViewModels.Entries;
using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Views.Controls.Entries
{
    public partial class SshKeyDetailsView : UserControl
    {
        public SshKeyDetailsView()
        {
            InitializeComponent();
        }

        private void View_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // The card reuses this view when another key is selected: hide its secrets again.
            RevealPrivateKey.IsChecked = false;
            RevealPassphrase.IsChecked = false;
        }

        private void SavePrivateKey_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SshKeyViewModel { HasPrivateKey: true } key)
                return;

            var keyType = key.KeyInfo?.KeyType;
            var fileName = keyType switch
            {
                "ssh-ed25519" => "id_ed25519",
                "ssh-rsa" => "id_rsa",
                _ when keyType?.StartsWith("ecdsa-", StringComparison.Ordinal) == true => "id_ecdsa",
                _ => "id_key"
            };
            var dialog = new SaveFileDialog
            {
                Filter = SshKeyFiles.Filter,
                FileName = fileName,
                InitialDirectory = SshKeyFiles.SshDirectory,
                AddExtension = false,
            };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true)
                return;

            try
            {
                // OpenSSH rejects keys with CRLF line endings or without a final newline.
                File.WriteAllText(dialog.FileName, SshKeys.NormalizePrivateKey(key.PrivateKey), new UTF8Encoding(false));
                EntryHost.FindViewModel(this)?.ShowStatus(LocalizationManager.Instance["PrivateKeySaved"]);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                MessageBox.Show(Window.GetWindow(this), ex.Message, LocalizationManager.Instance["SavePrivateKey"],
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
