using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace SinglePass.WPF.Views.Windows
{
    public partial class ProcessingDialogWindow : Window
    {
        private ProcessingDialogWindow(string title, string message)
        {
            InitializeComponent();
            Title = title;
            MessageText.Text = message;
        }

        internal static async Task RunAsync(string title, string message, Func<CancellationToken, Task> operation)
        {
            using var cancellation = new CancellationTokenSource();
            var dialog = new ProcessingDialogWindow(title, message) { Owner = Application.Current.MainWindow };
            var operationFinished = false;
            dialog.Closed += (_, _) =>
            {
                if (!operationFinished)
                    cancellation.Cancel();
            };

            // ShowDialog pumps the dispatcher while the operation runs.
            var pendingOperation = dialog.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    await operation(cancellation.Token);
                }
                finally
                {
                    operationFinished = true;
                    if (dialog.IsVisible)
                        dialog.Close();
                }
            }, DispatcherPriority.ContextIdle);

            try
            {
                dialog.ShowDialog();
            }
            finally
            {
                pendingOperation.Abort();
            }

            await pendingOperation.Task.Unwrap();
        }
    }
}
