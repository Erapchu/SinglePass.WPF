using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for MessageDialogWindow.xaml
    /// </summary>
    public partial class MessageDialogWindow : Window
    {
        public MessageDialogViewModel ViewModel { get; }
        public DialogChoice? Result { get; private set; }

        public MessageDialogWindow(MessageDialogViewModel vm)
        {
            InitializeComponent();

            vm.Accept += ViewModel_Accept;

            DataContext = vm;
            ViewModel = vm;
        }

        private void ViewModel_Accept(DialogChoice? result)
        {
            Result = result;
            DialogResult = true;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            ViewModel.Accept -= ViewModel_Accept;
        }
    }
}
