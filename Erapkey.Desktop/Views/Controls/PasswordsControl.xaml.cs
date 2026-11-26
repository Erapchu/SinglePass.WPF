using Erapkey.Core.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Erapkey.Desktop.ViewModels;
using Erapkey.Desktop.Views.Windows;

namespace Erapkey.Desktop.Views.Controls
{
    /// <summary>
    /// Interaction logic for PasswordsControl.xaml
    /// </summary>
    public partial class PasswordsControl : UserControl
    {
        internal const string CredentialDragFormat = "Erapkey.CredentialIds";

        private CredentialViewModel _dragCredential;
        private bool _deferSingleSelection;
        private Point _dragStart;
        private ScrollViewer _credentialsScrollViewer;

        public PasswordsControl()
        {
            InitializeComponent();
        }

        private async void AddPasswordButton_Click(object sender, RoutedEventArgs e)
        {
            // In a category the type is already chosen; elsewhere the menu asks for it.
            if (DataContext is PasswordsViewModel { SelectedCategory: { } category })
            {
                await AddItemAsync(category);
                return;
            }

            if (AddPasswordButton.ContextMenu is { } menu)
            {
                menu.PlacementTarget = AddPasswordButton;
                menu.IsOpen = true;
            }
        }

        private async void NewItemMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: CredentialType type })
                await AddItemAsync(type);
        }

        private async Task AddItemAsync(CredentialType type)
        {
            if (DataContext is not PasswordsViewModel viewModel || !viewModel.AddCommand.CanExecute(type))
                return;

            if (Window.GetWindow(this) is MainWindow window)
                await window.ShowNewCredentialEditorAsync(CredentialsDetailsControl, AddPasswordButton, viewModel, type);
            else
                await viewModel.AddCommand.ExecuteAsync(type);
        }

        private void EditPasswordMenuItem_Click(object sender, RoutedEventArgs e) => CredentialsDetailsControl.EditCredential();

        private async void DeletePasswordMenuItem_Click(object sender, RoutedEventArgs e) =>
            await CredentialsDetailsControl.DeleteCredentialAsync();

        private void CredentialsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(CredentialsListBox);
            var row = ItemsControl.ContainerFromElement(CredentialsListBox, e.OriginalSource as DependencyObject) as ListViewItem;
            _dragCredential = row?.Content as CredentialViewModel;
            _deferSingleSelection = _dragCredential != null && row.IsSelected &&
                CredentialsListBox.SelectedItems.Count > 1 && Keyboard.Modifiers == ModifierKeys.None;
            if (_deferSingleSelection)
                e.Handled = true;
        }

        private void CredentialsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_deferSingleSelection && _dragCredential is { } credential &&
                ItemsControl.ContainerFromElement(CredentialsListBox, e.OriginalSource as DependencyObject)
                    is ListViewItem row && ReferenceEquals(row.Content, credential))
            {
                CredentialsListBox.UnselectAll();
                row.IsSelected = true;
                row.Focus();
            }

            _dragCredential = null;
            _deferSingleSelection = false;
        }

        private void CredentialsList_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
                _dragCredential = null;
            if (_dragCredential is not { } credential)
                return;

            var offset = e.GetPosition(CredentialsListBox) - _dragStart;
            if (System.Math.Abs(offset.X) < SystemParameters.MinimumHorizontalDragDistance &&
                System.Math.Abs(offset.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragCredential = null;
            _deferSingleSelection = false;
            var ids = CredentialsListBox.SelectedItems.Contains(credential)
                ? CredentialsListBox.SelectedItems.OfType<CredentialViewModel>().Select(item => item.Model.Id).ToArray()
                : new[] { credential.Model.Id };
            var data = new DataObject(CredentialDragFormat, ids, autoConvert: false);
            DragDrop.DoDragDrop(CredentialsListBox, data, DragDropEffects.Move);
        }

        private CollectionViewGroup FirstGroup =>
            CredentialsListBox.GroupAt(0);

        private void CredentialsList_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.OriginalSource is not ScrollViewer viewer || viewer.TemplatedParent != CredentialsListBox)
                return;

            _credentialsScrollViewer = viewer;
        }

        private void CredentialsList_LayoutUpdated(object sender, System.EventArgs e)
        {
            var group = FirstGroup;
            if (_credentialsScrollViewer is { IsVisible: true, VerticalOffset: > 0 } viewer &&
                viewer.Template?.FindName("PART_ScrollContentPresenter", viewer) is ScrollContentPresenter viewport &&
                FindRowAtViewportTop(viewport, viewport) is { } row)
                group = CredentialsListBox.GroupAt(CredentialsListBox.ItemContainerGenerator.IndexFromContainer(row));

            StickyGroupHeaderContent.Content = group;
            StickyGroupHeaderContent.Visibility = group == null ? Visibility.Hidden : Visibility.Visible;
        }

        private static ListViewItem FindRowAtViewportTop(Visual parent, ScrollContentPresenter viewport)
        {
            // Only realized rows are visited; no off-screen containers are created.
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                if (VisualTreeHelper.GetChild(parent, index) is not Visual child)
                    continue;

                if (child is ListViewItem group)
                {
                    var top = group.TranslatePoint(new Point(), viewport).Y;
                    if (top < viewport.ActualHeight && top + group.ActualHeight > 0)
                        return group;
                }
                else if (FindRowAtViewportTop(child, viewport) is { } match)
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

        private void FavIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Image { IsVisible: true } icon)
                icon.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
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
