using System.Windows.Controls;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for CredentialsDetailsControl.xaml
    /// </summary>
    public partial class CredentialsDetailsControl : UserControl
    {
        public CredentialsDetailsControl()
        {
            InitializeComponent();
        }

        private void GroupPicker_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }
    }
}
