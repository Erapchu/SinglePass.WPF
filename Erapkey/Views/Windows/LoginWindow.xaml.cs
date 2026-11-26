using System.Windows;
using Erapkey.ViewModels;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Erapkey.Views.Windows
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        private LoginWindowViewModel ViewModel => DataContext as LoginWindowViewModel;

        public LoginWindow(LoginWindowViewModel loginWindowViewModel)
        {
            InitializeComponent();

            DataContext = loginWindowViewModel;
            loginWindowViewModel.Accept += LoginWindowViewModel_Accept;
        }

        private void LoginWindowViewModel_Accept()
        {
            DialogResult = true;
        }

        internal void ResetForSessionLock()
        {
            ViewModel.CancelLogin();
            ShowPasswordButton.IsChecked = false;
            LoginPasswordBox.Clear();
            RevealedPassword.Clear();
        }

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
                return;

            ViewModel.Password = passwordBox.Password;
        }

        private void ShowPassword_Changed(object sender, RoutedEventArgs e)
        {
            if (ShowPasswordButton.IsChecked == true)
            {
                RevealedPassword.Text = LoginPasswordBox.Password;
                RevealedPassword.Visibility = Visibility.Visible;
                LoginPasswordBox.Visibility = Visibility.Collapsed;
                PasswordLabel.Target = RevealedPassword;
                RevealedPassword.Focus();
                RevealedPassword.CaretIndex = RevealedPassword.Text.Length;
            }
            else
            {
                LoginPasswordBox.Visibility = Visibility.Visible;
                RevealedPassword.Visibility = Visibility.Collapsed;
                RevealedPassword.Clear();
                PasswordLabel.Target = LoginPasswordBox;
                LoginPasswordBox.Focus();
            }
        }

        private void RevealedPassword_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ShowPasswordButton.IsChecked == true && LoginPasswordBox.Password != RevealedPassword.Text)
                LoginPasswordBox.Password = RevealedPassword.Text;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            ViewModel.Accept -= LoginWindowViewModel_Accept;
            ViewModel.CancelLogin();
        }

        private void Window_SourceInitialized(object sender, System.EventArgs e)
        {
            ViewModel.OwnerHandle = new WindowInteropHelper(this).Handle;
        }

        private void Window_ContentRendered(object sender, System.EventArgs e)
        {
            // Offer Windows Hello right away; cancelling its prompt leaves the master password field ready.
            ContentRendered -= Window_ContentRendered;
            if (ViewModel.WindowsHelloOffered)
                ViewModel.UnlockWithWindowsHelloCommand.Execute(null);
        }

        private void Window_Activated(object sender, System.EventArgs e)
        {
            ViewModel.RefreshCapsLockCommand.Execute(null);
        }
    }
}
