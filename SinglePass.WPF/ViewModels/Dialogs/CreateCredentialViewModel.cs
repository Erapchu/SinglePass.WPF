using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.WPF.Enums;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    [INotifyPropertyChanged]
    public partial class CredentialEditViewModel
    {
        public event Action<MaterialDialogResult?> Accept;

        [ObservableProperty]
        private CredentialViewModel _credentialViewModel;

        [ObservableProperty]
        private string _dialogIdentifier;

        [ObservableProperty]
        private CredentialDetailsMode _mode;

        public string CaptionText => _mode switch
        {
            CredentialDetailsMode.Edit => SinglePass.Language.Properties.Resources.Edit,
            CredentialDetailsMode.New => SinglePass.Language.Properties.Resources.NewItem,
            _ => string.Empty,
        };

        public CredentialEditViewModel()
        {

        }

        [RelayCommand]
        private void Cancel()
        {
            Accept?.Invoke(MaterialDialogResult.None);
        }

        [RelayCommand]
        private void Ok()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            Accept?.Invoke(MaterialDialogResult.OK);
        }
    }
}
