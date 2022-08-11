using PasswordManager.ViewModels;
using System.Windows.Controls;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for ProcessingControl.xaml
    /// </summary>
    public partial class ProcessingControl : UserControl
    {
        public ProcessingViewModel ViewModel => DataContext as ProcessingViewModel;

        public ProcessingControl(string headText, string midText, string dialogIdentifier)
        {
            InitializeComponent();

            DataContext = new ProcessingViewModel(headText, midText, dialogIdentifier);
        }
    }
}
