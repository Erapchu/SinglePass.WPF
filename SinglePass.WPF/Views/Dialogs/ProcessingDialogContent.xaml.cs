using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows.Controls;

namespace SinglePass.WPF.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for ProcessingDialogContent.xaml
    /// </summary>
    public partial class ProcessingDialogContent : UserControl
    {
        public ProcessingViewModel ViewModel => DataContext as ProcessingViewModel;

        public ProcessingDialogContent(string headText, string midText, string dialogIdentifier)
        {
            InitializeComponent();

            DataContext = new ProcessingViewModel(headText, midText, dialogIdentifier);
        }
    }
}
