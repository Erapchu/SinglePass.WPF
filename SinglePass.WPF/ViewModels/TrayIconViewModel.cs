using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.WPF.Extensions;
using SinglePass.WPF.Views.Windows;
using System.Linq;

namespace SinglePass.WPF.ViewModels
{
    internal class TrayIconViewModel : ObservableRecipient
    {
        private RelayCommand _openMainWindowCommand;
        private RelayCommand _exitAppCommand;

        public RelayCommand OpenMainWindowCommand => _openMainWindowCommand ??= new RelayCommand(OpenMainWindow);
        public RelayCommand ExitAppCommand => _exitAppCommand ??= new RelayCommand(ExitApp);

        public TrayIconViewModel()
        {

        }

        private void OpenMainWindow()
        {
            var mainWindow = System.Windows.Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            mainWindow?.BringToFrontAndActivate();
        }

        private void ExitApp()
        {
            System.Windows.Application.Current.Shutdown();
        }
    }
}
