using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Extensions;
using PasswordManager.Views;
using System.Linq;
using System.Windows;

namespace PasswordManager.ViewModels
{
    internal class TrayIconViewModel : ObservableRecipient
    {
        public TrayIconViewModel()
        {
        }

        private void OpenMainWindow()
        {
            var searchWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            searchWindow?.BringToFrontAndActivate();
        }

        private void ExitApp()
        {
            Application.Current.Shutdown();
        }

        private RelayCommand _openMainWindowCommand;
        public RelayCommand OpenMainWindowCommand => _openMainWindowCommand
            ?? (_openMainWindowCommand = new RelayCommand(OpenMainWindow));

        private RelayCommand _exitAppCommand;
        public RelayCommand ExitAppCommand => _exitAppCommand
            ?? (_exitAppCommand = new RelayCommand(ExitApp));
    }
}
