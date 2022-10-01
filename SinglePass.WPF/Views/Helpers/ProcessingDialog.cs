using MaterialDesignThemes.Wpf;
using SinglePass.WPF.Views.Dialogs;
using System.Threading;
using System.Threading.Tasks;

namespace SinglePass.WPF.Views.Helpers
{
    internal static class ProcessingDialog
    {
        /// <summary>
        /// Shows processing dialog content. To return cancellation token to cancel some operation
        /// don't await this call.
        /// </summary>
        /// <param name="headText">Head text.</param>
        /// <param name="midText">Main text.</param>
        /// <param name="dialogIdentifier"><see cref="DialogHost"/> identifier where need to show materialized message box. It's analog of window's HWND.</param>
        /// <param name="cancellationToken">Cancellation token that will be cancelled if user clikc Cancel button in dialog.</param>
        /// <returns></returns>
        public static Task Show(
            string headText,
            string midText,
            string dialogIdentifier,
            out CancellationToken cancellationToken)
        {
            var processingControl = new ProcessingDialogContent(
                headText,
                midText,
                dialogIdentifier);
            cancellationToken = processingControl.ViewModel.CancellationToken;
            return DialogHost.Show(processingControl, dialogIdentifier);
        }
    }
}
