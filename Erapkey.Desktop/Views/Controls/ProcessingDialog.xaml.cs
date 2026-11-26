using Erapkey.Desktop.Views.Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Erapkey.Desktop.Views.Controls
{
    /// <summary>
    /// Shows progress of a long operation the user can cancel. Shown by <see cref="ModalHost"/>.
    /// </summary>
    public partial class ProcessingDialog : UserControl, IModalDialog
    {
        private ProcessingDialog(string title, string message)
        {
            InitializeComponent();
            HeaderText.Text = title;
            MessageText.Text = message;
        }

        public event Action CloseRequested;

        FrameworkElement IModalDialog.Footer => Footer;

        public IInputElement InitialFocus => CancelButton;

        public void Cancel() => CloseRequested?.Invoke();

        private void Cancel_Click(object sender, RoutedEventArgs e) => Cancel();

        /// <summary>
        /// Runs <paramref name="operation"/> while the dialog is shown. Cancelling or dismissing
        /// the dialog cancels the operation's token; the dialog closes when the operation ends.
        /// </summary>
        /// <exception cref="OperationCanceledException">The user cancelled and the operation observed it.</exception>
        internal static async Task RunAsync(string title, string message, Func<CancellationToken, Task> operation)
        {
            using var cancellation = new CancellationTokenSource();
            var dialog = new ProcessingDialog(title, message);
            var shown = ModalDialog.ShowAsync(dialog);
            var running = RunOperationAsync();

            async Task RunOperationAsync()
            {
                try
                {
                    await operation(cancellation.Token);
                }
                finally
                {
                    dialog.CloseRequested?.Invoke();
                }
            }

            await shown;
            if (!running.IsCompleted)
                cancellation.Cancel();
            await running;
        }
    }
}
