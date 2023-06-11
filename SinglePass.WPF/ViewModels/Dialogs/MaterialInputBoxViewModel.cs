using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    public partial class MaterialInputBoxViewModel : ObservableObject
    {
        private static readonly Lazy<MaterialInputBoxViewModel> _lazy = new(() => new MaterialInputBoxViewModel());
        public static MaterialInputBoxViewModel DesignTimeInstance => _lazy.Value;

        public string DialogIdentifier { get; set; }

        [ObservableProperty]
        private string _hint;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        private string _inputedText;

        [ObservableProperty]
        private string _header;

        [ObservableProperty]
        private bool _isPassword;

        public MaterialInputBoxViewModel()
        {

        }

        [RelayCommand(CanExecute = nameof(CanAccept))]
        private void Accept()
        {
            if (DialogHost.IsDialogOpen(DialogIdentifier))
                DialogHost.Close(DialogIdentifier, InputedText);
        }

        private bool CanAccept()
        {
            return !string.IsNullOrWhiteSpace(InputedText);
        }

        [RelayCommand]
        private void Cancel()
        {
            if (DialogHost.IsDialogOpen(DialogIdentifier))
                DialogHost.Close(DialogIdentifier);
        }
    }
}
