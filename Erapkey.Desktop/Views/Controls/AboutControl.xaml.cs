using Erapkey.Core.Localization;
using Erapkey.Desktop.ViewModels.Dialogs;
using Erapkey.Desktop.Views.Helpers;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Erapkey.Desktop.Views.Controls
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
                _ = MessageDialog.ShowAsync(localization["Error"], localization["AboutOpenLinkFailed"], MessageDialogButtons.OK);
            }
        }
    }
}
