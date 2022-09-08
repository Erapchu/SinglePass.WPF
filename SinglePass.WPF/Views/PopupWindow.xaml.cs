using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels;
using System;

namespace SinglePass.WPF.Views
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : MaterialWindow
    {
        public bool IsClosed { get; private set; }

        private PopupViewModel ViewModel { get; }

        public IntPtr ForegroundHWND
        {
            get => ViewModel.ForegroundHWND;
            set => ViewModel.ForegroundHWND = value;
        }

        public PopupWindow(PopupViewModel popupViewModel)
        {
            InitializeComponent();
            
            popupViewModel.Accept += PopupViewModel_Accept;
            DataContext = popupViewModel;
            ViewModel = popupViewModel;
        }

        private void PopupViewModel_Accept()
        {
            Close();
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (!IsClosed)
                Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            IsClosed = true;
        }
    }
}
