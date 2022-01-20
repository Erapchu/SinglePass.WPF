using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.Input;
using NLog;
using PasswordManager.Collections;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Views;
using PasswordManager.Views.MessageBox;
using System;
using System.Threading.Tasks;

namespace PasswordManager.ViewModels
{
    public class PasswordsViewModel : NavigationItemViewModel
    {
        #region Design time instance
        private static readonly Lazy<PasswordsViewModel> _lazy = new(GetDesignTimeVM);
        public static PasswordsViewModel DesignTimeInstance => _lazy.Value;

        private static PasswordsViewModel GetDesignTimeVM()
        {
            var vm = new PasswordsViewModel();
            var cred = new Credential();
            cred.NameField.Value = "Test";
            cred.LoginField.Value = "TestLogin";
            cred.PasswordField.Value = "TestPass";
            cred.OtherField.Value = "TestOther";
            var credVm = new CredentialViewModel(cred);
            vm.Credentials.Add(credVm);
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;

        public event Action<CredentialViewModel, CredentialsDialogMode, bool> FlyoutRequested;

        public ObservableCollectionDelayed<CredentialViewModel> Credentials { get; } = new ObservableCollectionDelayed<CredentialViewModel>();

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                SetProperty(ref _selectedCredential, value);
                FlyoutRequested?.Invoke(value, CredentialsDialogMode.View, true);
            }
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(SettingsService settingsService, ILogger logger)
        {
            _settingsService = settingsService;
            _logger = logger;

            Name = "Credentials";
            ItemIndex = PasswordsNavigationItemIndex;
            IconKind = PackIconKind.Password;
        }

        public async Task LoadCredentialsAsync()
        {
            try
            {
                Loading = true;
                await _settingsService.LoadCredentialsAsync();
                var credentials = _settingsService.Credentials;
                using var delayed = Credentials.DelayNotifications();
                foreach (var cred in credentials)
                {
                    delayed.Add(new CredentialViewModel(cred));
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
            finally
            {
                Loading = false;
            }
        }

        private void AddCredential()
        {
            var credentialVM = new CredentialViewModel(new Credential());
            FlyoutRequested?.Invoke(credentialVM, CredentialsDialogMode.New, true);
        }

        private void EditCredential(CredentialViewModel credVM)
        {
            var cloneVM = credVM.Clone();
            FlyoutRequested?.Invoke(cloneVM, CredentialsDialogMode.Edit, true);
        }

        private async Task DeleteCredentialAsync(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                "Delete credential?",
                $"Name: {credVM.NameFieldVM.Value}",
                MaterialMessageBoxButtons.YesNo,
                MvvmHelper.MainWindowDialogName,
                PackIconKind.Delete);
            if (result == MaterialDialogResult.Yes)
            {
                await _settingsService.DeleteCredential(credVM.Model);
                Credentials.Remove(credVM);
            }
        }

        private void CopyToClipboard(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            try
            {
                System.Windows.Clipboard.SetText(data);
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        private RelayCommand _addCredentialCommand;
        public RelayCommand AddCredentialCommand => _addCredentialCommand ??= new RelayCommand(AddCredential);

        private RelayCommand<CredentialViewModel> _editCredentialCommand;
        public RelayCommand<CredentialViewModel> EditCredentialCommand => _editCredentialCommand ??= new RelayCommand<CredentialViewModel>(EditCredential);

        private AsyncRelayCommand<CredentialViewModel> _deleteCredentialCommand;
        public AsyncRelayCommand<CredentialViewModel> DeleteCredentialCommand => _deleteCredentialCommand ??= new AsyncRelayCommand<CredentialViewModel>(DeleteCredentialAsync);

        private RelayCommand<string> _copyToClipboardCommand;
        public RelayCommand<string> CopyToClipboardCommand => _copyToClipboardCommand ??= new RelayCommand<string>(CopyToClipboard);
    }
}
