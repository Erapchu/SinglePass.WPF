using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;

namespace PasswordManager.Views.InputBox
{
    internal class MaterialInputBoxViewModel : ObservableObject
    {
        private static readonly Lazy<MaterialInputBoxViewModel> _lazy = new(() => new MaterialInputBoxViewModel("Header", "Example", null));
        public static MaterialInputBoxViewModel DesignTimeInstance => _lazy.Value;

        private string _hint;
        private string _inputedText;
        private string _header;
        private readonly string _dialogIdentifier;
        private RelayCommand _cancelCommand;
        private RelayCommand _acceptCommand;

        public string Hint
        {
            get => _hint;
            set
            {
                if (_hint == value)
                    return;

                _hint = value;
                OnPropertyChanged();
            }
        }

        public string InputedText
        {
            get => _inputedText;
            set
            {
                _inputedText = value;
                OnPropertyChanged();
                AcceptCommand.NotifyCanExecuteChanged();
            }
        }

        public string Header
        {
            get => _header;
            set
            {
                if (_header == value)
                    return;

                _header = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand AcceptCommand => _acceptCommand ??= new RelayCommand(Accept, CanAccept);

        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);

        public MaterialInputBoxViewModel(string header, string hint, string dialogIdentifier)
        {
            _header = header;
            _hint = hint;
            _dialogIdentifier = dialogIdentifier;
        }

        private bool CanAccept()
        {
            return !string.IsNullOrWhiteSpace(InputedText);
        }

        private void Accept()
        {
            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier, InputedText);
        }

        private void Cancel()
        {
            if (DialogHost.IsDialogOpen(_dialogIdentifier))
                DialogHost.Close(_dialogIdentifier);
        }
    }
}
