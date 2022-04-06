using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.Views.InputBox
{
    /// <summary>
    /// Interaction logic for MaterialInputBoxContent.xaml
    /// </summary>
    public partial class MaterialInputBoxContent : UserControl
    {
        private readonly bool _passwordInput;
        private MaterialInputBoxViewModel ViewModel => DataContext as MaterialInputBoxViewModel;

        public MaterialInputBoxContent(bool passwordInput)
        {
            InitializeComponent();
            _passwordInput = passwordInput;

            if (_passwordInput)
                PasswordInputBox.Visibility = Visibility.Visible;
            else
                TextInputBox.Visibility = Visibility.Visible;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(200);
            if (_passwordInput)
                PasswordInputBox.Focus();
            else
                TextInputBox.Focus();
        }

        private void PasswordInputBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.InputedText = PasswordInputBox.Password;
        }
    }
}
