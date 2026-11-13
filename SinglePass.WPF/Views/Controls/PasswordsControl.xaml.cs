using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace SinglePass.WPF.Views.Controls
{
    /// <summary>
    /// Interaction logic for PasswordsControl.xaml
    /// </summary>
    public partial class PasswordsControl : UserControl
    {
        private ScrollViewer _credentialsScrollViewer;

        public PasswordsControl()
        {
            InitializeComponent();
        }

        private CollectionViewGroup FirstGroup =>
            CredentialsListBox.Items.Groups is { Count: > 0 } groups ? groups[0] as CollectionViewGroup : null;

        private void CredentialsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is not ScrollViewer viewer || viewer.TemplatedParent != CredentialsListBox)
                return;

            _credentialsScrollViewer = viewer;
            if (viewer.ScrollableHeight <= 0 ||
                viewer.Template?.FindName("PART_VerticalScrollBar", viewer) is not ScrollBar { Track: { Thumb: { } thumb } track } ||
                double.IsNaN(track.ViewportSize))
                return;

            // A virtualized panel estimates its extent. Size the thumb independently of that estimate.
            track.ViewportSize = double.NaN;
            thumb.Height = 32;
            // Fixed-size track layout doesn't restore visibility after the empty list hid the track.
            track.Visibility = Visibility.Visible;
        }

        private void CredentialsList_LayoutUpdated(object sender, System.EventArgs e)
        {
            var group = FirstGroup;
            if (_credentialsScrollViewer is { IsVisible: true, VerticalOffset: > 0 } viewer &&
                viewer.Template?.FindName("PART_ScrollContentPresenter", viewer) is ScrollContentPresenter viewport &&
                FindGroupAtViewportTop(viewport, viewport) is { Content: CollectionViewGroup visibleGroup })
                group = visibleGroup;

            StickyGroupHeaderContent.Content = group;
            StickyGroupHeaderContent.Visibility = group == null ? Visibility.Hidden : Visibility.Visible;
        }

        private static GroupItem FindGroupAtViewportTop(Visual parent, ScrollContentPresenter viewport)
        {
            // The virtualizing panel only provides containers for realized groups.
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                if (VisualTreeHelper.GetChild(parent, index) is not Visual child)
                    continue;

                if (child is GroupItem group)
                {
                    var top = group.TranslatePoint(new Point(), viewport).Y;
                    if (top <= 0 && top + group.ActualHeight > 0)
                        return group;
                }
                else if (FindGroupAtViewportTop(child, viewport) is { } match)
                {
                    return match;
                }
            }

            return null;
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
