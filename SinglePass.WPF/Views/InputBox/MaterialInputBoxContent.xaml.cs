using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.InputBox
{
    /// <summary>
    /// Interaction logic for MaterialInputBoxContent.xaml
    /// </summary>
    public partial class MaterialInputBoxContent : UserControl
    {
        private readonly bool _isPassword;
        private MaterialInputBoxViewModel ViewModel => DataContext as MaterialInputBoxViewModel;

        public MaterialInputBoxContent(bool isPassword)
        {
            InitializeComponent();
            _isPassword = isPassword;

            if (_isPassword)
                PasswordInputBox.Visibility = Visibility.Visible;
            else
                TextInputBox.Visibility = Visibility.Visible;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(200);
            if (_isPassword)
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
