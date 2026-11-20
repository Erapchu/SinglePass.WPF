using System;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Asks for the password of the cloud vault file during synchronization. Shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class SyncPasswordDialog : UserControl, IModalDialog
    {
        public SyncPasswordDialog()
        {
            InitializeComponent();
        }

        /// <summary>The entered password; <c>null</c> if the dialog was cancelled.</summary>
        internal string Password { get; private set; }

        public event Action CloseRequested;

        FrameworkElement IModalDialog.Footer => Footer;

        public IInputElement InitialFocus => PasswordInput;

        public void Cancel() => CloseRequested?.Invoke();

        private void PasswordInput_Changed(object sender, RoutedEventArgs e)
        {
            AcceptButton.IsEnabled = !string.IsNullOrWhiteSpace(PasswordInput.Password);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordInput.Password;
            CloseRequested?.Invoke();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Cancel();
    }
}
