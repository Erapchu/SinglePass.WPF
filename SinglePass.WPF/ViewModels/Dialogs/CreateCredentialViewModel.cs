using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SinglePass.WPF.Enums;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    public partial class CredentialEditViewModel : ObservableObject
    {
        public event Action<DialogChoice?> Accept;

        [ObservableProperty]
        private CredentialViewModel _credentialViewModel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CaptionText))]
        private CredentialDetailsMode _mode;

        public string CaptionText => Mode switch
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
            Accept?.Invoke(DialogChoice.None);
        }

        [RelayCommand]
        private void Ok()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            Accept?.Invoke(DialogChoice.OK);
        }
    }
}
