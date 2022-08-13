using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using System;
using System.Threading;

namespace SinglePass.WPF.ViewModels
{
    [INotifyPropertyChanged]
    public partial class ProcessingViewModel
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
        private readonly CancellationTokenSource _processingCTS;

        [ObservableProperty]
        private string _headText;

        [ObservableProperty]
        private string _midText;

        public CancellationToken CancellationToken => _processingCTS.Token;

        private ProcessingViewModel() { }

        public ProcessingViewModel(string processingText, string midText, string dialogIdentifier)
        {
            _headText = processingText;
            _midText = midText;
            _dialogIdentifier = dialogIdentifier;
            _processingCTS = new CancellationTokenSource();
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogHost.Close(_dialogIdentifier);
            _processingCTS.Cancel();
        }
    }
}
