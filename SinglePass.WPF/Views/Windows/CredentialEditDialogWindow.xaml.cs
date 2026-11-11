using System.Windows;
using SinglePass.WPF.ViewModels.Dialogs;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for CredentialEditDialogWindow.xaml
    /// </summary>
    public partial class CredentialEditDialogWindow : Window
    {
        public CredentialEditViewModel ViewModel { get; }
        public DialogChoice? Result { get; private set; }

        public CredentialEditDialogWindow()
        {
            InitializeComponent();
        }

        public CredentialEditDialogWindow(CredentialEditViewModel credentialEditViewModel)
        {
            InitializeComponent();

            credentialEditViewModel.Accept += CredentialEditViewModel_Accept;
            ViewModel = credentialEditViewModel;
            DataContext = ViewModel;
        }

        private void CredentialEditViewModel_Accept(DialogChoice? result)
        {
            Result = result;
            DialogResult = true;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            ViewModel.Accept -= CredentialEditViewModel_Accept;
        }
    }
}
