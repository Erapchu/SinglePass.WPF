using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SinglePass.WPF.ViewModels.Dialogs;

namespace SinglePass.WPF.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for MaterialInputBoxContent.xaml
    /// </summary>
    public partial class MaterialInputBoxContent : UserControl
    {
        public MaterialInputBoxViewModel ViewModel { get; }

        public MaterialInputBoxContent(MaterialInputBoxViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(200);
            if (ViewModel.IsPassword)
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
