using Erapkey.ViewModels;
using Erapkey.Views.Windows;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace Erapkey.Views.Controls
{
    /// <summary>
    /// Details card of the selected entry, or the editor of a draft. Shows the fields every entry has;
    /// the fields of the entry type come from the views and editors in <c>Views/Controls/Entries</c>.
    /// </summary>
    public partial class CredentialsDetailsControl : UserControl
    {
        public static readonly DependencyProperty DisplayedDraftProperty = DependencyProperty.Register(
            nameof(DisplayedDraft), typeof(CredentialViewModel), typeof(CredentialsDetailsControl),
            new PropertyMetadata(null, (sender, e) => ((CredentialsDetailsControl)sender).SetValue(IsEditingPropertyKey, e.NewValue != null)));

        private static readonly DependencyPropertyKey IsEditingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsEditing), typeof(bool), typeof(CredentialsDetailsControl), new PropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty = IsEditingPropertyKey.DependencyProperty;

        public CredentialViewModel DisplayedDraft
        {
            get => (CredentialViewModel)GetValue(DisplayedDraftProperty);
            set => SetValue(DisplayedDraftProperty, value);
        }

        public bool IsEditing => (bool)GetValue(IsEditingProperty);

        public CredentialsDetailsControl()
        {
            InitializeComponent();
            FollowDraft();
        }

        internal void FollowDraft() => SetBinding(DisplayedDraftProperty, new Binding(nameof(PasswordsViewModel.DraftCredential)));

        internal double MeasureEditorHeight(double width)
        {
            Measure(new Size(width, double.PositiveInfinity));
            return DesiredSize.Height;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e) => EditCredential();

        internal void EditCredential()
        {
            if (DataContext is not PasswordsViewModel viewModel || !viewModel.EditCommand.CanExecute(null))
                return;

            if (Window.GetWindow(this) is MainWindow window && Parent is Decorator anchor)
                window.ShowCredentialEditor(this, anchor, EditButton, viewModel);
            else
                viewModel.EditCommand.Execute(null);
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e) => await DeleteCredentialAsync();

        internal async Task DeleteCredentialAsync()
        {
            if (DataContext is not PasswordsViewModel viewModel || !viewModel.DeleteCommand.CanExecute(null))
                return;

            if (Window.GetWindow(this) is MainWindow window && Parent is Decorator anchor)
                window.ShowCredentialDeletion(this, anchor, DeleteButton, viewModel);
            else
                await viewModel.DeleteCommand.ExecuteAsync(null);
        }

        internal void UseModalSurface(bool enabled)
        {
            if (enabled)
            {
                CardBorder.Background = Brushes.Transparent;
                CardBorder.BorderBrush = Brushes.Transparent;
            }
            else
            {
                CardBorder.ClearValue(Border.BackgroundProperty);
                CardBorder.ClearValue(Border.BorderBrushProperty);
            }
        }

        internal void UseDefaultIcon() => CredentialIcon.SetResourceReference(Image.SourceProperty, "DefaultFavIcon");

        private void CredentialIcon_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is Image { IsVisible: true } icon)
                icon.GetBindingExpression(Image.SourceProperty)?.UpdateTarget();
        }

        private void NameEditBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is not true)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                if (!NameEditBox.IsVisible)
                    return;

                EditScrollViewer.ScrollToTop();
                NameEditBox.Focus();
            }, DispatcherPriority.Input);
        }
    }
}
