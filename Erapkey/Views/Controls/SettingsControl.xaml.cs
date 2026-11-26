using System.Windows.Controls;
using System.Windows.Input;

namespace Erapkey.Views.Controls
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public static RoutedCommand OpenCloudSyncCommand { get; } = new();
        public static RoutedCommand OpenSecurityCommand { get; } = new();
        public static RoutedCommand OpenAppearanceCommand { get; } = new();
        public static RoutedCommand OpenAboutCommand { get; } = new();

        public SettingsControl()
        {
            InitializeComponent();
        }
    }
}
