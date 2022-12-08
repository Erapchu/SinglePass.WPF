using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Windows;

namespace SinglePass.WPF.Views.Helpers
{
    public static class MaterialMessageBox
    {
        /// <summary>
        /// Shows material dialog window.
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="content">Main content.</param>
        /// <param name="buttons">Specifies constants defining which buttons to display on a Material Dialog.</param>
        /// <returns>One of the <see cref="MaterialDialogResult"/> values.</returns>
        public static MaterialDialogResult ShowDialog(
            string header,
            string content,
            MaterialMessageBoxButtons buttons,
            PackIconKind? packIconKind = null)
        {
            var services = (System.Windows.Application.Current as App).Services;
            var mainWindow = services.GetService<MainWindow>();
            var dialogWindow = services.GetService<MaterialMessageBoxDialogWindow>();
            dialogWindow.Owner = mainWindow;
            var vm = dialogWindow.ViewModel;
            vm.Header = header;
            vm.Content = content;
            vm.MaterialMessageBoxButtons = buttons;
            vm.IconKind = packIconKind;
            dialogWindow.ShowDialog();
            var result = dialogWindow.Result;

            return result is null ? MaterialDialogResult.None : (MaterialDialogResult)result;
        }
    }
}
