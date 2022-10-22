using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using SinglePass.WPF.Enums;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    [INotifyPropertyChanged]
    public partial class CreateCredentialViewModel
    {
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

        public CreateCredentialViewModel()
        {

        }

        [RelayCommand]
        private void Cancel()
        {
            if (DialogHost.IsDialogOpen(DialogIdentifier))
                DialogHost.Close(DialogIdentifier, MaterialDialogResult.Cancel);
        }

        [RelayCommand]
        private void Ok()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            if (DialogHost.IsDialogOpen(DialogIdentifier))
                DialogHost.Close(DialogIdentifier, MaterialDialogResult.OK);
        }
    }
}
