using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for CreateCredentialView.xaml
    /// </summary>
    public partial class CreateCredentialView : UserControl
    {
        public CreateCredentialViewModel ViewModel { get; set; }

        public CreateCredentialView(CreateCredentialViewModel createCredentialViewModel)
        {
            InitializeComponent();
            ViewModel = createCredentialViewModel;
            DataContext = createCredentialViewModel;
        }
    }
}
