using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for CreateCredentialView.xaml
    /// </summary>
    public partial class CredentialEditContent : UserControl
    {
        public CredentialEditViewModel ViewModel { get; set; }

        public CredentialEditContent(CredentialEditViewModel credentialEditViewModel)
        {
            InitializeComponent();
            ViewModel = credentialEditViewModel;
            DataContext = credentialEditViewModel;
        }
    }
}
