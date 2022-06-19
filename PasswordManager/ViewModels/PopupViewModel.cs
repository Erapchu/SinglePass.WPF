using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Helpers;
using PasswordManager.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;

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
        private readonly ILogger<PopupViewModel> _logger;
        private readonly CredentialViewModelFactory _credentialViewModelFactory;
        private readonly List<CredentialViewModel> _credentials = new();

        public event Action Accept;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();

        private CredentialViewModel _selectedCredentialVM;
        public CredentialViewModel SelectedCredentialVM
        {
            get => _selectedCredentialVM;
            set => SetProperty(ref _selectedCredentialVM, value);
        }

        private RelayCommand<PassFieldViewModel> _setAndCloseCommand;
        public RelayCommand<PassFieldViewModel> SetAndCloseCommand => _setAndCloseCommand ??= new RelayCommand<PassFieldViewModel>(SetAndClose);

        private RelayCommand _closeCommand;
        public RelayCommand CloseCommand => _closeCommand ??= new RelayCommand(Close);

        private AsyncRelayCommand _loadedCommand;
        public AsyncRelayCommand LoadedCommand => _loadedCommand ??= new AsyncRelayCommand(Loaded);

        public IntPtr ForegroundHWND { get; set; }

        private PopupViewModel() { }

        public PopupViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<PopupViewModel> logger,
            CredentialViewModelFactory credentialViewModelFactory)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _credentialViewModelFactory = credentialViewModelFactory;
        }

        private void Close()
        {
            Accept?.Invoke();
        }

        private void SetAndClose(PassFieldViewModel passFieldViewModel)
        {
            try
            {
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

                Accept?.Invoke();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }
        }

        private Task Loaded()
        {
            return Task.Run(() =>
            {
                _credentials.Clear();
                _credentials.AddRange(_credentialsCryptoService.Credentials
                    .Select(cr => _credentialViewModelFactory.ProvideNew(cr))
                    .ToList());
                var tempList = new List<CredentialViewModel>(_credentials);

                // Extract browsers address bar string
                try
                {
                    // https://stackoverflow.com/questions/18897070/getting-the-current-tabs-url-from-google-chrome-using-c-sharp

                    _ = WinApiProvider.GetWindowThreadProcessId(ForegroundHWND, out uint processId);
                    var process = Process.GetProcessById((int)processId);
                    var mwh = process.MainWindowHandle;
                    if (mwh == IntPtr.Zero)
                        return;

                    AutomationElement element = AutomationElement.FromHandle(mwh);
                    if (element is null)
                        return;

                    Condition conditions = new AndCondition(
                        new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id),
                        new PropertyCondition(AutomationElement.IsControlElementProperty, true),
                        new PropertyCondition(AutomationElement.IsContentElementProperty, true),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));

                    AutomationElement elementx = element.FindFirst(TreeScope.Descendants, conditions);
                    var addressBarString = ((ValuePattern)elementx.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;

                    if (!addressBarString.StartsWith("http"))
                        addressBarString = "http://" + addressBarString;

                    if (Uri.TryCreate(addressBarString, UriKind.Absolute, out Uri addressBarUri))
                    {
                        var host = addressBarUri.Host;
                        var domains = host.Split('.', StringSplitOptions.RemoveEmptyEntries);
                        // Take last 2 levels of domains
                        var domainsString = string.Join('.', domains.TakeLast(2));

                        tempList = tempList
                            .OrderByDescending(c => c.SiteFieldVM.Value is null ? -1 : c.SiteFieldVM.Value.Contains(domainsString) ? 1 : -1)
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(null, ex);
                }
                finally
                {
                    DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(tempList);
                    OnPropertyChanged(nameof(DisplayedCredentials));
                }
            });
        }
    }
}
