using Erapkey.Core.Localization;
using Erapkey.Desktop.Services;
using Erapkey.Desktop.ViewModels.Entries;
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Desktop.Views.Controls.Entries
{
    public partial class LoginEditor : UserControl
    {
        private bool _readingTotpQr;

        public LoginEditor()
        {
            InitializeComponent();
        }

        private LoginViewModel Draft => DataContext as LoginViewModel;

        private void Editor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // A PasswordBox cannot be bound: start each draft with an empty setup field.
            TotpSetupBox.Clear();
            TotpQrStatus.Text = string.Empty;
        }

        private void ReplaceTotp_Click(object sender, RoutedEventArgs e)
        {
            if (Draft is { } draft)
            {
                draft.ReplaceTotp = true;
                TotpSetupBox.Focus();
            }
        }

        private void RemoveTotp_Click(object sender, RoutedEventArgs e)
        {
            if (Draft is { } draft)
                draft.RemoveTotp = true;
        }

        private void TotpSetupBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            TotpQrStatus.Text = string.Empty;
            if (Draft is { } draft)
                draft.TotpSetupInput = TotpSetupBox.Password;
        }

        private async void PasteTotpQr_Click(object sender, RoutedEventArgs e)
        {
            if (_readingTotpQr || Draft is not { RemoveTotp: false } draft)
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
                    !ReferenceEquals(Draft, draft) || draft.TotpSetupInput != previousInput)
                    return;

                if (link != null)
                    TotpSetupBox.Password = link;
                ShowQrStatus(link != null ? "TotpQrImported" : "InvalidTotpQr");
            }
            catch (Exception ex) when (ex is ExternalException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                if (TotpSetupBox.IsVisible && ReferenceEquals(Draft, draft))
                    ShowQrStatus("InvalidTotpQr");
            }
            finally
            {
                _readingTotpQr = false;
            }
        }

        private void ShowQrStatus(string resourceKey) => TotpQrStatus.Text = LocalizationManager.Instance[resourceKey];
    }
}
