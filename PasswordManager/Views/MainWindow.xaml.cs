using Autofac;
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
    public partial class MainWindow : Window
    {
        private ILifetimeScope _scope;

        public MainWindow(ILifetimeScope scope)
        {
            InitializeComponent();
            _scope = scope;
            DataContext = scope.Resolve<MainWindowViewModel>();
        }

        private async void Window_Closed(object sender, EventArgs e)
        {
            await _scope.DisposeAsync();
            _scope = null;
        }
    }
}
