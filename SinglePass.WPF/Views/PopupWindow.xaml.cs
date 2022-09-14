using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

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

        private void MaterialWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => SearchTextBox.Focus());
        }

        private void MaterialWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            var isEnterDown = e.Key == Key.Enter;

            if (isEnterDown)
            {
                if (isShiftDown)
                {
                    // This is Enter + Shift
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.PasswordFieldVM);
                }
                else
                {
                    // This is Enter
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.LoginFieldVM);
                }
            }

            if (e.Key == System.Windows.Input.Key.Escape)
                Close();
        }
    }
}
