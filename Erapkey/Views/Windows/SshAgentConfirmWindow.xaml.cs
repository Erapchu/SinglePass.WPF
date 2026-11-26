using Erapkey.Core.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Erapkey.Views.Windows
{
    /// <summary>
    /// Asks whether a program may use an SSH key. Enter and Escape deny, so that a keystroke meant for
    /// another window cannot allow it; without an answer the request is denied after a minute.
    /// </summary>
    public partial class SshAgentConfirmWindow : Window
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);
        private readonly DispatcherTimer _timeout;

        public bool Allowed { get; private set; }
        public bool Remember => Allowed && RememberCheckBox.IsChecked == true;

        public SshAgentConfirmWindow(string keyName, PipeClient client)
        {
            InitializeComponent();
            KeyName.Text = keyName;
            ClientName.Text = client.DisplayName;
            ClientPath.Text = client.Chain.FirstOrDefault()?.Path;
            ClientPath.Visibility = string.IsNullOrEmpty(ClientPath.Text) ? Visibility.Collapsed : Visibility.Visible;

            _timeout = new DispatcherTimer { Interval = Timeout };
            _timeout.Tick += (_, _) => Close();
            _timeout.Start();
            Closed += (_, _) => _timeout.Stop();
        }

        private void Allow_Click(object sender, RoutedEventArgs e)
        {
            Allowed = true;
            Close();
        }

        private void Deny_Click(object sender, RoutedEventArgs e) => Close();
    }
}
