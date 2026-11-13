using System.Windows.Controls;
using System.Windows.Input;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public static RoutedCommand OpenCloudSyncCommand { get; } = new();

        public SettingsControl()
        {
            InitializeComponent();
        }
    }
}
