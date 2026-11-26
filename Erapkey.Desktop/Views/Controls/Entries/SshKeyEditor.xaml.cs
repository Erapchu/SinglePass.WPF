using Microsoft.Win32;
using Erapkey.Core.Localization;
using Erapkey.Core.Services;
using Erapkey.Desktop.ViewModels.Entries;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Desktop.Views.Controls.Entries
{
    public partial class SshKeyEditor : UserControl
    {
        public SshKeyEditor()
        {
            InitializeComponent();
        }

        private SshKeyViewModel Draft => DataContext as SshKeyViewModel;

        private void Editor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => ShowStatus(null);

        private void ShowStatus(string resourceKey) =>
            SshKeyStatus.Text = resourceKey == null ? string.Empty : LocalizationManager.Instance[resourceKey];

        private void ImportSshKey_Click(object sender, RoutedEventArgs e)
        {
            if (Draft is not { } draft)
                return;

            var dialog = new OpenFileDialog { Filter = SshKeyFiles.Filter, InitialDirectory = SshKeyFiles.SshDirectory };
            if (dialog.ShowDialog(Window.GetWindow(this)) != true || !ReferenceEquals(Draft, draft))
                return;

            try
            {
                var path = dialog.FileName;
                var isPublicFile = path.EndsWith(".pub", StringComparison.OrdinalIgnoreCase);
                var keyPath = isPublicFile ? path[..^4] : path;
                var text = SshKeyFiles.Read(path);
                string privateKey = null, publicKey = null;
                if (SshKeys.TryParsePublicKey(text, out _, out _, out _))
                {
                    // A public key was picked: take the private key from the file next to it, if there is one.
                    publicKey = text.Trim();
                    if (isPublicFile && File.Exists(keyPath))
                        privateKey = SshKeyFiles.Read(keyPath);
                }
                else if (text.Contains("PRIVATE KEY-----", StringComparison.Ordinal))
                {
                    privateKey = text;
                    var publicPath = path + ".pub";
                    if (File.Exists(publicPath) && SshKeyFiles.Read(publicPath) is var publicText &&
                        SshKeys.TryParsePublicKey(publicText, out _, out _, out _))
                        publicKey = publicText.Trim();
                }
                else
                {
                    ShowStatus("InvalidSshKeyFile");
                    return;
                }

                // The public key goes last: setting the private key may replace it with one without a comment.
                if (privateKey != null)
                    draft.PrivateKey = SshKeys.NormalizePrivateKey(privateKey);
                if (publicKey != null)
                    draft.PublicKey = publicKey;
                if (EntryHost.FindCard(this)?.DisplayedDraft is { NameFieldVM.HasText: false } entry)
                    entry.NameFieldVM.Value = Path.GetFileName(keyPath);
                ShowStatus(null);
            }
            catch (InvalidDataException)
            {
                ShowStatus("SshKeyFileTooLarge");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowStatus("InvalidSshKeyFile");
            }
        }

        private void GenerateSshKey_Click(object sender, RoutedEventArgs e)
        {
            if (Draft is not { HasPrivateKey: false } draft)
                return;

            // Same default comment as ssh-keygen.
            var (privateKey, publicKey) = SshKeys.GenerateEd25519($"{Environment.UserName}@{Environment.MachineName}");
            draft.PrivateKey = privateKey;
            draft.PublicKey = publicKey;
            draft.Passphrase = null;
            ShowStatus(null);
        }
    }
}
