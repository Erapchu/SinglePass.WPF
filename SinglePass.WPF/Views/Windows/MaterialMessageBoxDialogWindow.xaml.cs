using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels.Dialogs;
using System.Windows;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Interaction logic for MaterialMessageBoxDialogWindow.xaml
    /// </summary>
    public partial class MaterialMessageBoxDialogWindow : MaterialWindow
    {
        public MaterialMessageBoxViewModel ViewModel { get; set; }
        public MaterialDialogResult? Result { get; private set; }

        public MaterialMessageBoxDialogWindow()
        {
            InitializeComponent();
        }

        public MaterialMessageBoxDialogWindow(MaterialMessageBoxViewModel vm)
        {
            InitializeComponent();

            vm.Accept += ViewModel_Accept;

            DataContext = vm;
            ViewModel = vm;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() =>
            {
                InvalidateMeasure();
                InvalidateArrange();
            });
        }

        private void ViewModel_Accept(MaterialDialogResult? result)
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
