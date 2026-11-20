using SinglePass.WPF.Localization;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace SinglePass.WPF.Views.Controls
{
    public partial class AboutControl : UserControl
    {
        public static string AppVersion { get; } = typeof(AboutControl).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion.Split('+', 2)[0];

        public AboutControl()
        {
            InitializeComponent();
        }

        private void OpenLink(object sender, RequestNavigateEventArgs e)
        {
            e.Handled = true;
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                var localization = LocalizationManager.Instance;
                MessageDialog.ShowDialog(localization["Error"], localization["AboutOpenLinkFailed"], MessageDialogButtons.OK);
            }
        }
    }
}
