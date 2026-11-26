using Erapkey.Views.Controls;
using Erapkey.Views.Windows;
using System.Threading.Tasks;
using System.Windows;

namespace Erapkey.Views.Helpers
{
    internal static class ModalDialog
    {
        /// <summary>
        /// Shows a dialog card inside the main window.
        /// </summary>
        /// <param name="dialog">The card; read its result after the task completes.</param>
        /// <param name="focusReturn">Element focused after the dialog closes; defaults to the currently focused element.</param>
        /// <returns>A task that completes when the dialog closes. Completes at once, without showing anything,
        /// when there is no main window or it already shows another modal: treat that as cancelled.</returns>
        public static Task ShowAsync(IModalDialog dialog, FrameworkElement focusReturn = null) =>
            Application.Current.MainWindow is MainWindow mainWindow
                ? mainWindow.ShowDialogAsync(dialog, focusReturn)
                : Task.CompletedTask;
    }
}
