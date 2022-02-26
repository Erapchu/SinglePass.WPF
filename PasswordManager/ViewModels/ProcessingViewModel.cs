using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Threading;

namespace PasswordManager.ViewModels
{
    public class ProcessingViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<ProcessingViewModel> _lazy = new(GetDesignTimeVM);
        public static ProcessingViewModel DesignTimeInstance => _lazy.Value;

        private static ProcessingViewModel GetDesignTimeVM()
        {
            var vm = new ProcessingViewModel();
            vm.ProcessingText = "Authorizing...";
            return vm;
        }
        #endregion

        private readonly string _dialogIdentifier;
        private string _processingText;
        private RelayCommand _cancelCommand;
        private CancellationTokenSource _processingCTS;

        public string ProcessingText
        {
            get => _processingText;
            set => SetProperty(ref _processingText, value);
        }

        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);
        public CancellationToken CancellationToken => _processingCTS.Token;

        private ProcessingViewModel() { }

        public ProcessingViewModel(string processingText, string dialogIdentifier)
        {
            _processingText = processingText;
            _dialogIdentifier = dialogIdentifier;
            _processingCTS = new CancellationTokenSource();
        }

        private void Cancel()
        {
            DialogHost.Close(_dialogIdentifier);
            _processingCTS.Cancel();
        }
    }
}
