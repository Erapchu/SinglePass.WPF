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
            vm.HeadText = "Authorizing...";
            vm.MidText = "Please wait!";
            return vm;
        }
        #endregion

        private readonly string _dialogIdentifier;
        private string _headText;
        private string _midText;
        private RelayCommand _cancelCommand;
        private CancellationTokenSource _processingCTS;

        public string HeadText
        {
            get => _headText;
            set => SetProperty(ref _headText, value);
        }

        public string MidText
        {
            get => _midText;
            set => SetProperty(ref _midText, value);
        }

        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(Cancel);
        public CancellationToken CancellationToken => _processingCTS.Token;

        private ProcessingViewModel() { }

        public ProcessingViewModel(string processingText, string midText, string dialogIdentifier)
        {
            _headText = processingText;
            _midText = midText;
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
