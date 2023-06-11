using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Collections;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Unidecode.NET;

namespace SinglePass.WPF.ViewModels
{
    public partial class PopupViewModel : ObservableObject
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
        private readonly List<CredentialViewModel> _credentialVMs = new();

        public event Action Accept;
        public event Action<CredentialViewModel> ScrollIntoViewRequired;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();

        [ObservableProperty]
        private CredentialViewModel _selectedCredentialVM;

        [ObservableProperty]
        private bool _isLoading;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = DisplayCredentialsAsync();
            }
        }

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
        private void SetFullAndClose(CredentialViewModel credentialViewModel)
        {
            try
            {
                Accept?.Invoke();

                WindowsClipboard.SetText(credentialViewModel.LoginFieldVM.Value);

                INPUT[] inputs = new INPUT[4];

                // Ctrl+V
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

                var uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);

                // Pause between Ctrl+V and set clipboard
                Thread.Sleep(100);

                WindowsClipboard.SetText(credentialViewModel.PasswordFieldVM.Value);

                inputs = new INPUT[6];

                // Tab
                inputs[0].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[0].U.ki.wVk = WindowsKeyboard.VK_TAB;

                inputs[1].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[1].U.ki.wVk = WindowsKeyboard.VK_TAB;
                inputs[1].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

                // Ctrl+V
                inputs[2].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[2].U.ki.wVk = WindowsKeyboard.VK_CONTROL;

                inputs[3].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[3].U.ki.wVk = WindowsKeyboard.VK_V;

                inputs[4].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[4].U.ki.wVk = WindowsKeyboard.VK_V;
                inputs[4].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

                inputs[5].type = WindowsKeyboard.INPUT_KEYBOARD;
                inputs[5].U.ki.wVk = WindowsKeyboard.VK_CONTROL;
                inputs[5].U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;

                uSent = WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
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
            return Task.Run(async () =>
            {
                var tempCredentialsVM = new List<CredentialViewModel>();
                try
                {
                    IsLoading = true;
                    tempCredentialsVM.AddRange(_credentialViewModelFactory.ProvideAllNew());

                    var addressBarString = _addressBarExtractor.ExtractAddressBar(ForegroundHWND);
                    if (!string.IsNullOrWhiteSpace(addressBarString))
                    {
                        if (!addressBarString.StartsWith("http"))
                            addressBarString = "http://" + addressBarString;

                        if (Uri.TryCreate(addressBarString, UriKind.Absolute, out Uri addressBarUri))
                        {
                            var host = addressBarUri.Host;
                            var domains = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                            // Take last 2 levels of domains
                            var domainsString = string.Join('.', domains.TakeLast(2));

                            tempCredentialsVM = tempCredentialsVM
                                .OrderByDescending(c => c.SiteFieldVM.Value is null ? -1 : c.SiteFieldVM.Value.Contains(domainsString) ? 1 : -1)
                                .ThenByDescending(c => c.LastModifiedTime)
                                .ToList();
                        }
                    }
                    else
                    {
                        tempCredentialsVM = tempCredentialsVM
                            .OrderByDescending(c => c.LastModifiedTime)
                            .ToList();
                    }

                    _credentialVMs.AddRange(tempCredentialsVM);
                    await DisplayCredentialsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, null);
                }
                finally
                {
                    IsLoading = false;
                }
            });
        }

        private async Task DisplayCredentialsAsync()
        {
            try
            {
                List<CredentialViewModel> filteredCredentials = null;
                var filterText = SearchText;

                if (string.IsNullOrEmpty(filterText))
                {
                    List<CredentialViewModel> tempList = null;
                    await Task.Run(() =>
                    {
                        tempList = _credentialVMs.ToList();
                    });
                    filteredCredentials = tempList;
                }
                else
                {
                    filteredCredentials = await Task.Run(() =>
                    {
                        string transliteratedText = null;
                        if (Regex.IsMatch(filterText, @"\p{IsCyrillic}"))
                        {
                            // there is at least one cyrillic character in the string
                            transliteratedText = filterText.Unidecode();
                        }
                        var translitCompare = !string.IsNullOrWhiteSpace(transliteratedText);

                        var fCreds = new List<CredentialViewModel>();
                        foreach (var cred in _credentialVMs)
                        {
                            var nameValue = cred.NameFieldVM.Value;
                            var loginValue = cred.LoginFieldVM.Value;
                            var websiteValue = cred.SiteFieldVM.Value;
                            var otherValue = cred.OtherFieldVM.Value;

                            if (nameValue.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1
                                || (loginValue != null && loginValue.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1)
                                || (websiteValue != null && websiteValue.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1)
                                || (otherValue != null && otherValue.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1)
                                || (translitCompare && nameValue.IndexOf(transliteratedText, StringComparison.OrdinalIgnoreCase) != -1))
                            {
                                fCreds.Add(cred);
                            }
                        }

                        return fCreds;
                    });
                }

                DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(filteredCredentials);
                OnPropertyChanged(nameof(DisplayedCredentials));

                // Selected credential always first according to search request
                SelectedCredentialVM = DisplayedCredentials.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private void HandleSearchKey(KeyEventArgs args)
        {
            if (args is null)
                return;

            switch (args.Key)
            {
                case Key.Up:
                    {
                        // Select previous
                        var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredentialVM);
                        if (selectedIndex != -1 && selectedIndex > 0)
                        {
                            SelectedCredentialVM = DisplayedCredentials[selectedIndex - 1];
                            ScrollIntoViewRequired?.Invoke(SelectedCredentialVM);
                        }
                        break;
                    }
                case Key.Down:
                    {
                        // Select next
                        var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredentialVM);
                        if (selectedIndex != -1 && selectedIndex < DisplayedCredentials.Count - 1)
                        {
                            SelectedCredentialVM = DisplayedCredentials[selectedIndex + 1];
                            ScrollIntoViewRequired?.Invoke(SelectedCredentialVM);
                        }
                        break;
                    }
            }
        }
    }
}
