using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;

namespace PasswordManager.Views.MessageBox
{
    public static class MaterialMessageBox
    {
        /// <summary>
        /// Shows materialized analog of standard MessageBox. To use this method you need to set <see cref="DialogHost"/> instance in your window
        /// (typically this may be specified towards the root of a Window's XAML).
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="content">Main content.</param>
        /// <param name="buttons">Specifies constants defining which buttons to display on a Material Dialog.</param>
        /// <param name="dialogIdentifier"><see cref="DialogHost"/> identifier where need to show materialized message box. It's analog of window's HWND.</param>
        /// <returns>One of the <see cref="MaterialDialogResult"/> values.</returns>
        public static async Task<MaterialDialogResult> ShowAsync(
            string header,
            string content,
            MaterialMessageBoxButtons buttons,
            string dialogIdentifier,
            PackIconKind? packIconKind = null)
        {
            var instance = new MaterialMessageBoxContent
            {
                DataContext = new MaterialMessageBoxViewModel(header, content, buttons, dialogIdentifier, packIconKind)
            };
            var result = await DialogHost.Show(instance, dialogIdentifier);

            return result is null ? MaterialDialogResult.None : (MaterialDialogResult)result;
        }
    }
}
