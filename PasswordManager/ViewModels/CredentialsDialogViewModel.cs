using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Enums;
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

        public event Action<CredentialViewModel, CredentialsDialogMode> Accept;
        public event Action<CredentialViewModel> Delete;
        public event Action Cancel;

        private CredentialViewModel _credentialViewModel;
        public CredentialViewModel CredentialViewModel
        {
            get => _credentialViewModel;
            set => SetProperty(ref _credentialViewModel, value);
        }

        public string CaptionText
        {
            get
            {
                switch (_mode)
                {
                    case CredentialsDialogMode.Edit:
                        return "Edit";
                    case CredentialsDialogMode.New:
                        return "New";
                    case CredentialsDialogMode.View:
                        return "Details";
                    default:
                        break;
                }

                return string.Empty;
            }
        }

        private CredentialsDialogMode _mode = CredentialsDialogMode.View;
        public CredentialsDialogMode Mode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                OnPropertyChanged(nameof(CaptionText));
            }
        }

        private bool _isPasswordVisible;
        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set => SetProperty(ref _isPasswordVisible, value);
        }

        public CredentialsDialogViewModel()
        {

        }

        private void OkExecute()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            Accept?.Invoke(CredentialViewModel, Mode);
        }

        private void CancelExecute()
        {
            Mode = CredentialsDialogMode.View;
            Cancel?.Invoke();
        }

        private void EditExecute()
        {
            if (CredentialViewModel is null)
                return;

            CredentialViewModel = CredentialViewModel.Clone();
            Mode = CredentialsDialogMode.Edit;
            IsPasswordVisible = true;
        }

        private void DeleteExecute()
        {
            if (CredentialViewModel is null)
                return;

            Delete?.Invoke(CredentialViewModel);
        }

        private RelayCommand _okCommand;
        public RelayCommand OkCommand => _okCommand ??= new RelayCommand(OkExecute);

        private RelayCommand _cancelCommand;
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(CancelExecute);

        private RelayCommand _editCommand;
        public RelayCommand EditCommand => _editCommand ??= new RelayCommand(EditExecute);

        private RelayCommand _deleteCommand;
        public RelayCommand DeleteCommand => _deleteCommand ??= new RelayCommand(DeleteExecute);
    }
}
