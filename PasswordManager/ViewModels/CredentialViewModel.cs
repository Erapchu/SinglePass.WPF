using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class CredentialViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<CredentialViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialViewModel DesignTimeInstance => _lazy.Value;

        private static CredentialViewModel GetDesignTimeVM()
        {
            var additionalFields = new List<PassField>() { new PassField() { Name = "Design additional field", Value = "Test value" } };
            var model = new Credential()
            {
                AdditionalFields = additionalFields
            };
            var vm = new CredentialViewModel(model);
            return vm;
        }
        #endregion

        public Credential Model { get; }
        public PassFieldViewModel NameFieldVM { get; }
        public PassFieldViewModel LoginFieldVM { get; }
        public PassFieldViewModel PasswordFieldVM { get; }
        public PassFieldViewModel OtherFieldVM { get; }
        public ObservableCollection<PassFieldViewModel> AdditionalFields { get; }

        private bool _isExpanded;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        public CredentialViewModel(Credential credential)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            NameFieldVM = new PassFieldViewModel(credential.NameField);
            LoginFieldVM = new PassFieldViewModel(credential.LoginField);
            PasswordFieldVM = new PassFieldViewModel(credential.PasswordField);
            OtherFieldVM = new PassFieldViewModel(credential.OtherField);
            AdditionalFields = new ObservableCollection<PassFieldViewModel>(credential.AdditionalFields.Select(f => new PassFieldViewModel(f)));
        }

        internal CredentialViewModel Clone()
        {
            var cloneModel = Model.Clone();
            var cloneVM = new CredentialViewModel(cloneModel);
            return cloneVM;
        }

        private void OkExecute(bool value)
        {
            NameFieldVM.ValidateValue();
            if (NameFieldVM.HasErrors)
                return;

            DialogHost.Close(MvvmHelper.MainWindowDialogName, value);
        }

        private RelayCommand<bool> _okCommand;
        public RelayCommand<bool> OkCommand => _okCommand ??= new RelayCommand<bool>(OkExecute);
    }
}
