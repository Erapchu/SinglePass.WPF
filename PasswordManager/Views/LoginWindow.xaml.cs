using PasswordManager.Controls;
using PasswordManager.ViewModels;

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
        }

        private async void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            var loadingCommand = ViewModel.LoadingCommand;
            loadingCommand.Cancel();
            //TODO: send password somehow here
            await loadingCommand.ExecuteAsync(null);
            if (ViewModel.Success)
            {
                DialogResult = true;
            }
        }
    }
}
