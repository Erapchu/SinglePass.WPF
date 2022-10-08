using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;

namespace SinglePass.WPF.ViewModels.Dialogs
{
    [INotifyPropertyChanged]
    internal partial class MaterialInputBoxViewModel
    {
        private static readonly Lazy<MaterialInputBoxViewModel> _lazy = new(() => new MaterialInputBoxViewModel("Header", "Example", null));
        public static MaterialInputBoxViewModel DesignTimeInstance => _lazy.Value;

        private readonly string _dialogIdentifier;

        [ObservableProperty]
        private string _hint;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
        private string _inputedText;

        [ObservableProperty]
        private string _header;

        public MaterialInputBoxViewModel(string header, string hint, string dialogIdentifier)
        {
            _header = header;
            _hint = hint;
            _dialogIdentifier = dialogIdentifier;
        }

        [RelayCommand(CanExecute = nameof(CanAccept))]
        private void Accept()
        {
            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier, InputedText);
        }

        private bool CanAccept()
        {
            return !string.IsNullOrWhiteSpace(InputedText);
        }

        [RelayCommand]
        private void Cancel()
        {
            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier);
        }
    }
}
