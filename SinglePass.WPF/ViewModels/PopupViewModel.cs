using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Collections;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SinglePass.WPF.ViewModels
{
    [INotifyPropertyChanged]
    public partial class PopupViewModel
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

        private readonly ILogger<PopupViewModel> _logger;
        private readonly CredentialViewModelFactory _credentialViewModelFactory;
        private readonly AddressBarExtractor _addressBarExtractor;

        public event Action Accept;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();

        [ObservableProperty]
        private CredentialViewModel _selectedCredentialVM;

        [ObservableProperty]
        private bool _isLoading;

        public IntPtr ForegroundHWND { get; set; }

        private PopupViewModel() { }

        public PopupViewModel(
            ILogger<PopupViewModel> logger,
            CredentialViewModelFactory credentialViewModelFactory,
            AddressBarExtractor addressBarExtractor)
        {
            _logger = logger;
            _credentialViewModelFactory = credentialViewModelFactory;
            _addressBarExtractor = addressBarExtractor;
        }

        [RelayCommand]
        private void Close()
        {
            Accept?.Invoke();
        }

        [RelayCommand]
        private void SetAndClose(PassFieldViewModel passFieldViewModel)
        {
            try
            {
                Accept?.Invoke();

                var inputData = passFieldViewModel.Value;
                if (!string.IsNullOrWhiteSpace(inputData))
                {
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
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        [RelayCommand]
        private Task Loading()
        {
            return Task.Run(() =>
            {
                var tempCredentialsVM = new List<CredentialViewModel>();
                try
                {
                    IsLoading = true;
                    tempCredentialsVM.AddRange(_credentialViewModelFactory.ProvideAllNew());

                    var addressBarString = _addressBarExtractor.ExtractAddressBar(ForegroundHWND);
                    if (!addressBarString.StartsWith("http"))
                        addressBarString = "http://" + addressBarString;

                    if (Uri.TryCreate(addressBarString, UriKind.Absolute, out Uri addressBarUri))
                    {
                        var host = addressBarUri.Host;
                        var domains = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                        // Take last 2 levels of domains
                        var domainsString = string.Join('.', domains.TakeLast(2));

                        tempCredentialsVM = tempCredentialsVM
                        .Where(c => c.SiteFieldVM.Value != null && c.SiteFieldVM.Value.Contains(domainsString))
                        .OrderByDescending(c => c.LastModifiedTime)
                        .ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
                finally
                {
                    DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(tempCredentialsVM);
                    OnPropertyChanged(nameof(DisplayedCredentials));
                    IsLoading = false;
                }
            });
        }
    }
}
