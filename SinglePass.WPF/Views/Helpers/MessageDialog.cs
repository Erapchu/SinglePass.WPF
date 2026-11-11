using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Windows;
using System.Windows;

namespace SinglePass.WPF.Views.Helpers
{
    public static class MessageDialog
    {
        /// <summary>
        /// Shows a modal message dialog.
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
            var viewModel = new MessageDialogViewModel
            {
                Header = header,
                Content = content,
                Buttons = buttons,
                Glyph = glyph
            };
            var dialog = new MessageDialogWindow(viewModel) { Owner = Application.Current.MainWindow };
            dialog.ShowDialog();
            return dialog.Result ?? DialogChoice.None;
        }
    }
}
