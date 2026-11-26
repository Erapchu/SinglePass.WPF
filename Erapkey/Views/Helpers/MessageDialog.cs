using Erapkey.ViewModels.Dialogs;
using Erapkey.Views.Windows;
using System.Threading.Tasks;
using System.Windows;

namespace Erapkey.Views.Helpers
{
    public static class MessageDialog
    {
        /// <summary>
        /// Shows a modal message dialog in a separate native window.
        /// Prefer <see cref="ShowAsync"/>, which shows the dialog inside the main window.
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="content">Main content.</param>
        /// <param name="buttons">Specifies constants defining which buttons to display in the dialog.</param>
        /// <param name="glyph">Optional Segoe MDL2 Assets glyph.</param>
        /// <returns>One of the <see cref="DialogChoice"/> values.</returns>
        public static DialogChoice ShowDialog(
            string header,
            string content,
            MessageDialogButtons buttons,
            string glyph = null)
        {
            var viewModel = CreateViewModel(header, content, buttons, glyph);
            return ShowWindow(viewModel);
        }

        /// <summary>
        /// Shows a message dialog inside the main window. Falls back to a native dialog window
        /// when the main window is hidden or already shows another modal.
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="content">Main content.</param>
        /// <param name="buttons">Specifies constants defining which buttons to display in the dialog.</param>
        /// <param name="glyph">Optional Segoe MDL2 Assets glyph.</param>
        /// <param name="focusReturn">Element focused after the dialog closes.</param>
        /// <returns>One of the <see cref="DialogChoice"/> values.</returns>
        public static Task<DialogChoice> ShowAsync(
            string header,
            string content,
            MessageDialogButtons buttons,
            string glyph = null,
            FrameworkElement focusReturn = null)
        {
            var viewModel = CreateViewModel(header, content, buttons, glyph);
            if (Application.Current.MainWindow is MainWindow mainWindow && mainWindow.CanShowModal)
                return mainWindow.ShowMessageAsync(viewModel, focusReturn);

            return Task.FromResult(ShowWindow(viewModel));
        }

        private static MessageDialogViewModel CreateViewModel(string header, string content, MessageDialogButtons buttons, string glyph) => new()
        {
            Header = header,
            Content = content,
            Buttons = buttons,
            Glyph = glyph
        };

        private static DialogChoice ShowWindow(MessageDialogViewModel viewModel)
        {
            var dialog = new MessageDialogWindow(viewModel) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();
            return dialog.Result ?? DialogChoice.None;
        }
    }
}
