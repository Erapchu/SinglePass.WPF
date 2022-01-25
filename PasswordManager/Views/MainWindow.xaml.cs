using PasswordManager.Controls;
using PasswordManager.ViewModels;
using System;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MaterialWindow
    {
        public MainWindow(MainWindowViewModel mainViewModel)
        {
            InitializeComponent();

            mainViewModel.CredentialSelected += Vm_CredentialSelected;
            DataContext = mainViewModel;
        }

        private void Vm_CredentialSelected(CredentialViewModel credVM)
        {
            var passStringLength = credVM?.PasswordFieldVM?.Value?.Length ?? 0;
            PasswordsControl.CredentialsDialog.PasswordFieldBox.Password = new string('*', passStringLength);
        }
    }
}
