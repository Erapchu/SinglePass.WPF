using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
