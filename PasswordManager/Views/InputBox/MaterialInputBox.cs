using MaterialDesignThemes.Wpf;
using System.Threading.Tasks;

namespace PasswordManager.Views.InputBox
{
    public static class MaterialInputBox
    {
        /// <summary>
        /// Shows materialized analog of input MessageBox. To use this method you need to set <see cref="DialogHost"/> instance in your window
        /// (typically this may be specified towards the root of a Window's XAML).
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="hint">Hint text.</param>
        /// <param name="dialogIdentifier"><see cref="DialogHost"/> identifier where need to show materialized message box. It's analog of window's HWND.</param>
        /// <returns>Inputed text by user or <see langword="null"/> in case if output is not string.</returns>
        public static async Task<string> ShowAsync(string header, string hint, string dialogIdentifier)
        {
            var instance = new MaterialInputBoxContent()
            {
                DataContext = new MaterialInputBoxViewModel(header, hint, dialogIdentifier)
            };
            var result = await DialogHost.Show(instance, dialogIdentifier);
            return result is string str ? str : null;
        }
    }
}
