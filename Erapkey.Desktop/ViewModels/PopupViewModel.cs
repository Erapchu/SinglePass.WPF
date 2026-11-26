using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Erapkey.Desktop.Collections;
using Erapkey.Desktop.Helpers;
using Erapkey.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Erapkey.Desktop.ViewModels
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
        private readonly List<CredentialViewModel> _credentialVMs = new();

        public event Action Accept;
        public event Action<CredentialViewModel> ScrollIntoViewRequired;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();

        [ObservableProperty]
        public partial CredentialViewModel SelectedCredentialVM { get; set; }

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        [ObservableProperty]
        public partial string SearchText { get; set; }

        partial void OnSearchTextChanged(string value) => _ = DisplayCredentialsAsync();

        /// <summary>
        /// The top-level window that was active when the popup opened. Credentials are pasted only into it.
        /// </summary>
        public IntPtr TargetWindow { get; set; }

        private static readonly TimeSpan TargetActivationTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan TargetActivationPollInterval = TimeSpan.FromMilliseconds(20);
        private static readonly TimeSpan PasteDelay = TimeSpan.FromMilliseconds(100);

        private static readonly INPUT[] CtrlV =
        [
            KeyInput(WindowsKeyboard.VK_CONTROL), KeyInput(WindowsKeyboard.VK_V),
            KeyInput(WindowsKeyboard.VK_V, up: true), KeyInput(WindowsKeyboard.VK_CONTROL, up: true)
        ];

        private static readonly INPUT[] TabCtrlV = [KeyInput(WindowsKeyboard.VK_TAB), KeyInput(WindowsKeyboard.VK_TAB, up: true), .. CtrlV];

        private PopupViewModel() { }

        public PopupViewModel(
            ILogger<PopupViewModel> logger,
            CredentialViewModelFactory credentialViewModelFactory)
        {
            _logger = logger;
            _credentialViewModelFactory = credentialViewModelFactory;
        }

        [RelayCommand]
        private void Close()
        {
            Accept?.Invoke();
        }

        [RelayCommand]
        private async Task SetFullAndCloseAsync(CredentialViewModel credentialViewModel)
        {
            try
            {
                Accept?.Invoke();
                if (!await ActivateTargetWindowAsync())
                    return;

                WindowsClipboard.SetText(credentialViewModel.Login.LoginFieldVM.Value);
                SendKeys(CtrlV);

                // Let the target read the login from the clipboard before it is replaced with the password
                await Task.Delay(PasteDelay);

                // Focus may have moved while the login was pasted: check again before the password leaves the vault
                if (!IsTargetWindowActive())
                {
                    _logger.LogWarning("The target window lost focus after the login was pasted; the password was not pasted.");
                    WindowsClipboard.ClearIfOwned();
                    return;
                }

                WindowsClipboard.SetText(credentialViewModel.Login.PasswordFieldVM.Value);
                SendKeys(TabCtrlV);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        [RelayCommand]
        private async Task SetAndCloseAsync(PassFieldViewModel passFieldViewModel)
        {
            try
            {
                Accept?.Invoke();

                var inputData = passFieldViewModel.Value;
                if (string.IsNullOrWhiteSpace(inputData) || !await ActivateTargetWindowAsync())
                    return;

                WindowsClipboard.SetText(inputData);
                SendKeys(CtrlV);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        /// <summary>
        /// Returns focus to <see cref="TargetWindow"/> after the popup closes and waits until it is the foreground window.
        /// Returns false when another window took the focus, so that nothing is pasted into it.
        /// </summary>
        private async Task<bool> ActivateTargetWindowAsync()
        {
            if (TargetWindow == IntPtr.Zero)
            {
                _logger.LogWarning("The window to paste into is unknown; nothing was pasted.");
                return false;
            }

            if (!IsTargetWindowActive())
                WinApiProvider.SetForegroundWindow(TargetWindow);

            var started = Stopwatch.GetTimestamp();
            while (!IsTargetWindowActive())
            {
                if (Stopwatch.GetElapsedTime(started) >= TargetActivationTimeout)
                {
                    _logger.LogWarning("Another window is in the foreground; nothing was pasted.");
                    return false;
                }

                await Task.Delay(TargetActivationPollInterval);
            }

            return true;
        }

        private bool IsTargetWindowActive() => WinApiProvider.GetForegroundWindow() == TargetWindow;

        private static void SendKeys(INPUT[] inputs)
        {
            WindowsKeyboard.SendInput((uint)inputs.Length, inputs, INPUT.Size);
        }

        private static INPUT KeyInput(byte virtualKey, bool up = false)
        {
            var input = new INPUT { type = WindowsKeyboard.INPUT_KEYBOARD };
            input.U.ki.wVk = virtualKey;
            if (up)
                input.U.ki.dwFlags = WindowsKeyboard.KEYEVENTF_KEYUP;
            return input;
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
                    // The popup types a login and password into the active window; other entry types have neither.
                    tempCredentialsVM.AddRange(_credentialViewModelFactory.ProvideAllNew().Where(c => c.IsLogin));

                    // The popup does not guess the site of the target window: in browsers the extension matches
                    // logins by the tab address (docs/browser-extension.md). Recently changed entries come first.
                    _credentialVMs.AddRange(tempCredentialsVM.OrderByDescending(c => c.LastModifiedTime));
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
                        var search = new CredentialSearch(filterText);
                        var fCreds = _credentialVMs.Where(search.Matches).ToList();

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
