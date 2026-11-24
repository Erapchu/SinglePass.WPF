using SinglePass.Core.Services.Browser;
using System.Windows;

namespace SinglePass.WPF.Views.Windows
{
    /// <summary>
    /// Asks whether a browser extension may connect, showing the code the extension shows too. Enter and Escape deny,
    /// so that a keystroke meant for another window cannot allow it; the server stops waiting after two minutes.
    /// </summary>
    public partial class BrowserPairingWindow : Window
    {
        public bool Allowed { get; private set; }

        public BrowserPairingWindow(BrowserPairingRequest request)
        {
            InitializeComponent();
            BrowserName.Text = request.Browser == "?" ? request.Name : $"{request.Name} ({request.Browser})";
            // 482 917: two groups are easier to compare.
            Code.Text = request.Code.Length == 6 ? request.Code[..3] + " " + request.Code[3..] : request.Code;
        }

        private void Allow_Click(object sender, RoutedEventArgs e)
        {
            Allowed = true;
            Close();
        }

        private void Deny_Click(object sender, RoutedEventArgs e) => Close();
    }
}
