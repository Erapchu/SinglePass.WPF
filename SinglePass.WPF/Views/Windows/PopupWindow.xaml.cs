using SinglePass.WPF.Controls;
using SinglePass.WPF.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace SinglePass.WPF.Views.Windows
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
            popupViewModel.ScrollIntoViewRequired += PopupViewModel_ScrollIntoViewRequired;
            DataContext = popupViewModel;
            ViewModel = popupViewModel;
        }

        private void PopupViewModel_ScrollIntoViewRequired(CredentialViewModel vm)
        {
            CredListBox.ScrollIntoView(vm);
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

        private void MaterialWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Dispatcher.InvokeAsync(() => SearchTextBox.Focus());
        }

        private void MaterialWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                return;
            }

            if (e.Key == Key.Enter)
            {
                // Enter
                ViewModel.SetFullAndCloseCommand.Execute(ViewModel.SelectedCredentialVM);
                return;
            }

            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                // Ctrl+1
                if (e.Key == Key.NumPad1 || e.Key == Key.D1)
                {
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.LoginFieldVM);
                    return;
                }

                // Ctrl+2
                if (e.Key == Key.NumPad2 || e.Key == Key.D2)
                {
                    ViewModel.SetAndCloseCommand.Execute(ViewModel.SelectedCredentialVM.PasswordFieldVM);
                    return;
                }
            }
        }

        private void MaterialWindow_Closed(object sender, EventArgs e)
        {
            ViewModel.Accept -= PopupViewModel_Accept;
            ViewModel.ScrollIntoViewRequired -= PopupViewModel_ScrollIntoViewRequired;
        }
    }
}
