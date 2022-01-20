using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class CredentialsDialogViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<CredentialsDialogViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialsDialogViewModel DesignTimeInstance => _lazy.Value;

        private static CredentialsDialogViewModel GetDesignTimeVM()
        {
            var additionalFields = new List<PassField>() { new PassField() { Name = "Design additional field", Value = "Test value" } };
            var model = new Credential()
            {
                AdditionalFields = additionalFields
            };
            var credVm = new CredentialViewModel(model);
            var vm = new CredentialsDialogViewModel(credVm, "Lorem ipsum dolor sit amet");
            return vm;
        }
        #endregion

        public CredentialViewModel CredentialViewModel { get; }

        public string CaptionText { get; }

        public CredentialsDialogViewModel(CredentialViewModel credentialViewModel, string captionText)
        {
            CredentialViewModel = credentialViewModel ?? throw new ArgumentNullException(nameof(credentialViewModel));
            CaptionText = captionText;
        }

        private void OkExecute(bool value)
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            DialogHost.Close(MvvmHelper.MainWindowDialogName, value);
        }

        private RelayCommand<bool> _okCommand;
        public RelayCommand<bool> OkCommand => _okCommand ??= new RelayCommand<bool>(OkExecute);
    }
}
