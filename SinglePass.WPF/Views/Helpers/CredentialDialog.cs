using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SinglePass.WPF.Enums;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Dialogs;
using System;
using System.Threading.Tasks;

namespace SinglePass.WPF.Views.Helpers
{
    public static class CredentialDialog
    {
        /// <summary>
        /// Shows materialized analog of input MessageBox. To use this method you need to set <see cref="DialogHost"/> instance in your window
        /// (typically this may be specified towards the root of a Window's XAML).
        /// </summary>
        /// <param name="header">Header of the dialog.</param>
        /// <param name="hint">Hint text.</param>
        /// <param name="dialogIdentifier"><see cref="DialogHost"/> identifier where need to show materialized message box. It's analog of window's HWND.</param>
        /// <param name="isPassword"><see langword="true"/> for password input.</param>
        /// <returns>Inputed text by user or <see langword="null"/> in case if output is not string.</returns>
        public static async Task<MaterialDialogResult> ShowAsync(
            CredentialViewModel credentialViewModel,
            string dialogIdentifier,
            CredentialDetailsMode mode)
        {
            var instance = (System.Windows.Application.Current as App).Services.GetService<CredentialEditContent>();
            instance.ViewModel.CredentialViewModel = credentialViewModel;
            instance.ViewModel.DialogIdentifier = dialogIdentifier;
            instance.ViewModel.Mode = mode;
            var result = await DialogHost.Show(instance, dialogIdentifier);
            return result is MaterialDialogResult res ? res : MaterialDialogResult.Cancel;
        }
    }
}
