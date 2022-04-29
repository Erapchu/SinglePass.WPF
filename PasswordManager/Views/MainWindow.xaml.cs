using PasswordManager.Controls;
using PasswordManager.Hotkeys;
using PasswordManager.ViewModels;
using System;
using System.Windows.Input;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MaterialWindow
    {
        private readonly HotkeysService _hotkeyService;

        public MainWindow(MainWindowViewModel mainViewModel, HotkeysService hotkeysService)
        {
            InitializeComponent();

            _hotkeyService = hotkeysService;

            mainViewModel.CredentialSelected += Vm_CredentialSelected;
            DataContext = mainViewModel;
        }

        private void Vm_CredentialSelected(CredentialViewModel credVM)
        {
            var passStringLength = credVM?.PasswordFieldVM?.Value?.Length ?? 0;
            PasswordsControl.CredentialsDialog.PasswordFieldBox.Password = new string('*', passStringLength);
            PasswordsControl.CredentialsListBox.ScrollIntoView(credVM);
        }

        private void MaterialWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //TODO: Shutdown process on main window close or hide window and cancel depend on settings
            Hide();
            ShowInTaskbar = false;
            e.Cancel = true;
        }

        private void ListBoxItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var anyCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);

            if (anyCtrlPressed)
                e.Handled = true;
        }

        private void MaterialWindow_Activated(object sender, EventArgs e)
        {
            _hotkeyService.IsEnabled = false;
        }

        private void MaterialWindow_Deactivated(object sender, EventArgs e)
        {
            _hotkeyService.IsEnabled = true;
        }
    }
}
