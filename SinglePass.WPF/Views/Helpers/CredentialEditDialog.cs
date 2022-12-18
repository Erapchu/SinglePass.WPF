using Microsoft.Extensions.DependencyInjection;
using SinglePass.WPF.Enums;
using SinglePass.WPF.ViewModels;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Windows;
using System;

namespace SinglePass.WPF.Views.Helpers
{
    public static class CredentialEditDialog
    {
        public static MaterialDialogResult ShowDialog(
            CredentialViewModel credentialViewModel,
            CredentialDetailsMode mode)
        {
            var services = (System.Windows.Application.Current as App).Services;
            var mainWindow = services.GetService<MainWindow>();
            var dialogWindow = services.GetService<CredentialEditDialogWindow>();
            dialogWindow.Owner = mainWindow;
            var vm = dialogWindow.ViewModel;
            vm.CredentialViewModel = credentialViewModel;
            vm.Mode = mode;
            dialogWindow.ShowDialog();
            var result = dialogWindow.Result;

            return result is null ? MaterialDialogResult.None : (MaterialDialogResult)result;
        }
    }
}
