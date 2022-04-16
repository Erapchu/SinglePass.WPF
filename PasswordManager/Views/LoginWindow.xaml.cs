using PasswordManager.Controls;
using PasswordManager.ViewModels;
using System.Windows.Controls;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : MaterialWindow
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

        private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is not PasswordBox passwordBox)
                return;

            ViewModel.Password = passwordBox.Password;
        }

        private void MaterialWindow_Closed(object sender, System.EventArgs e)
        {
            ViewModel.Accept -= LoginWindowViewModel_Accept;
        }

        private void MaterialWindow_Activated(object sender, System.EventArgs e)
        {
            ViewModel.RefreshCapsLockCommand.Execute(null);
        }
    }
}
