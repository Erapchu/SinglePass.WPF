using System.Windows;

namespace SinglePass.WPF.Views.Windows
{
    public partial class SyncPasswordDialogWindow : Window
    {
        internal SyncPasswordDialogWindow()
        {
            InitializeComponent();
        }

        internal string Password { get; private set; }

        private void PasswordInput_Changed(object sender, RoutedEventArgs e)
        {
            AcceptButton.IsEnabled = !string.IsNullOrWhiteSpace(PasswordInput.Password);
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            Password = PasswordInput.Password;
            DialogResult = true;
        }
    }
}
