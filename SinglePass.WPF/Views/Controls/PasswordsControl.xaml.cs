using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for PasswordsControl.xaml
    /// </summary>
    public partial class PasswordsControl : UserControl
    {
        public PasswordsControl()
        {
            InitializeComponent();
        }

        private void CredentialsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is not ScrollViewer viewer || viewer.ScrollableHeight <= 0 ||
                viewer.Template?.FindName("PART_VerticalScrollBar", viewer) is not ScrollBar { Track: { Thumb: { } thumb } track } ||
                double.IsNaN(track.ViewportSize))
                return;

            // A virtualized panel estimates its extent. Size the thumb independently of that estimate.
            track.ViewportSize = double.NaN;
            thumb.Height = 32;
            // Fixed-size track layout doesn't restore visibility after the empty list hid the track.
            track.Visibility = Visibility.Visible;
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.IsOpen = true;
            }
        }

        private async void UserControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool v && v)
            {
                await Task.Delay(50);
                SearchTextBox.Focus();
            }
        }
    }
}
