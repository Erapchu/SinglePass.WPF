using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Helpers;
using PasswordManager.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace PasswordManager.ViewModels
{
    public class PopupViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<PopupViewModel> _lazy = new(GetDesignTimeVM);
        public static PopupViewModel DesignTimeInstance => _lazy.Value;

        private static PopupViewModel GetDesignTimeVM()
        {
            var vm = new PopupViewModel();
            return vm;
        }
        #endregion

        private readonly CredentialsCryptoService _credentialsCryptoService;

        public event Action Accept;

        public ObservableCollection<CredentialViewModel> DisplayedCredentials { get; private set; }

        private CredentialViewModel _selectedCredentialVM;
        public CredentialViewModel SelectedCredentialVM
        {
            get => _selectedCredentialVM;
            set => SetProperty(ref _selectedCredentialVM, value);
        }

        private RelayCommand<CredentialViewModel> _setLoginAndCloseCommand;
        public RelayCommand<CredentialViewModel> SetLoginAndCloseCommand => _setLoginAndCloseCommand ??= new RelayCommand<CredentialViewModel>(SetLoginAndClose);

        private PopupViewModel() { }

        public PopupViewModel(CredentialsCryptoService credentialsCryptoService)
        {
            _credentialsCryptoService = credentialsCryptoService;

            var creds = _credentialsCryptoService.Credentials.Select(cr => new CredentialViewModel(cr)).ToList();
            DisplayedCredentials = new ObservableCollection<CredentialViewModel>(creds);
        }

        private void SetLoginAndClose(CredentialViewModel credentialViewModel)
        {
            var inputData = credentialViewModel.LoginFieldVM.Value;

            WindowsClipboard.SetText(inputData);

            INPUT[] inputs = new INPUT[4];

            inputs[0].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = WindowsKeyboard.VK_CONTROL;

            inputs[1].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = WindowsKeyboard.VK_V;

            inputs[2].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[2].U.ki.wVk = WindowsKeyboard.VK_V;
            inputs[2].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            inputs[3].type = WindowsKeyboard.INPUT_KEYBOARD;
            inputs[3].U.ki.wVk = WindowsKeyboard.VK_CONTROL;
            inputs[3].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

            // Send input simulate Ctrl + V
            var uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);

            Accept?.Invoke();
        }
    }
}
