using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;

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
            var vm = new CredentialsDialogViewModel();
            vm._credentialViewModel = credVm;
            vm.Mode = CredentialsDialogMode.View;
            return vm;
        }
        #endregion

        private CredentialViewModel _credentialViewModel;
        public CredentialViewModel CredentialViewModel
        {
            get => _credentialViewModel;
            set => SetProperty(ref _credentialViewModel, value);
        }

        private string _captionText;
        public string CaptionText
        {
            get => _captionText;
            private set => SetProperty(ref _captionText, value);
        }

        private CredentialsDialogMode _mode;
        public CredentialsDialogMode Mode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                switch (_mode)
                {
                    case CredentialsDialogMode.Edit:
                        CaptionText = "Edit";
                        break;
                    case CredentialsDialogMode.New:
                        CaptionText = "New";
                        break;
                    case CredentialsDialogMode.View:
                        CaptionText = "Details";
                        break;
                    default:
                        break;
                }
            }
        }

        public CredentialsDialogViewModel()
        {
            
        }

        private void OkExecute(bool value)
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

        }

        private void CancelExecute()
        {

        }

        private RelayCommand<bool> _okCommand;
        public RelayCommand<bool> OkCommand => _okCommand ??= new RelayCommand<bool>(OkExecute);

        private RelayCommand _cancelCommand;
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(CancelExecute);
    }

    public enum CredentialsDialogMode
    {
        Edit,
        New,
        View,
    }
}
