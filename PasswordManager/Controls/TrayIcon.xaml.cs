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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PasswordManager.Controls
{
    /// <summary>
    /// Interaction logic for TrayIcon.xaml
    /// </summary>
    public partial class TrayIcon : UserControl, IDisposable
    {
        private ContextMenu _trayIconContextMenu;
        private ContextMenu TrayIconContextMenu => _trayIconContextMenu ??= TryFindResource("TaskbarContextMenu") as ContextMenu;

        public TrayIcon()
        {
            InitializeComponent();
        }

        public void Dispose()
        {
            TaskbarIcon.Dispose();
        }

        private void TaskbarIcon_TrayContextMenuOpen(object sender, RoutedEventArgs e)
        {
            // To apply DynamicResource changes (related to issue on GitHub for TaskbarIcon: https://github.com/hardcodet/wpf-notifyicon/issues/19)
            TrayIconContextMenu?.UpdateDefaultStyle();
        }
    }
}
