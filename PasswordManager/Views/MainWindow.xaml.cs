using Autofac;
using PasswordManager.Controls;
using PasswordManager.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PasswordManager.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MaterialWindow
    {
        private ILifetimeScope _scope;

        public MainWindow(ILifetimeScope scope)
        {
            InitializeComponent();

            _scope = scope.BeginLifetimeScope();
            var vm = scope.Resolve<MainWindowViewModel>();
            vm.CredentialSelected += Vm_CredentialSelected;
            DataContext = vm;
        }

        private void Vm_CredentialSelected(CredentialViewModel credVM)
        {
            var passStringLength = credVM?.PasswordFieldVM?.Value?.Length ?? 0;
            PasswordsControl.CredentialsDialog.PasswordFieldBox.Password = new string('*', passStringLength);
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            await _scope.DisposeAsync();
            _scope = null;
        }
    }
}
