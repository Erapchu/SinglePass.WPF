using Autofac;
using MaterialDesignThemes.Wpf;
using Microsoft.Toolkit.Mvvm.ComponentModel;
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
    public class PasswordsViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<PasswordsViewModel> _lazy = new(GetDesignTimeVM);
        public static PasswordsViewModel DesignTimeInstance => _lazy.Value;

        private static PasswordsViewModel GetDesignTimeVM()
        {
            var vm = new PasswordsViewModel();
            return vm;
        }
        #endregion

        private readonly SettingsService _settingsService;
        private readonly ILogger _logger;

        private bool _loading;
        public bool Loading
        {
            get => _loading;
            set => SetProperty(ref _loading, value);
        }

        public ObservableCollectionDelayed<CredentialViewModel> Credentials { get; } = new ObservableCollectionDelayed<CredentialViewModel>();

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set => SetProperty(ref _selectedCredential, value);
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(SettingsService settingsService, ILogger logger)
        {
            _settingsService = settingsService;
            _logger = logger;
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

        private async Task AddCredentialAsync()
        {
            var credentialVM = new CredentialViewModel(new Credential());
            var credDialog = new CredentialsDialog
            {
                DataContext = credentialVM
            };
            var result = await DialogHost.Show(credDialog, MvvmHelper.MainWindowDialogName);
            if (result is bool boolResult && boolResult)
            {
                await _settingsService.AddCredential(credentialVM.Model);
                Credentials.Add(credentialVM);
            }
        }

        private async Task EditCredentialAsync(CredentialViewModel credVM)
        {
            var cloneVM = credVM.Clone();
            var credDialog = new CredentialsDialog
            {
                DataContext = cloneVM
            };
            var result = await DialogHost.Show(credDialog, MvvmHelper.MainWindowDialogName);
            if (result is bool boolResult && boolResult)
            {
                await _settingsService.EditCredential(cloneVM.Model);
                var currentIndex = Credentials.IndexOf(credVM);
                Credentials.Remove(credVM);
                Credentials.Insert(currentIndex, cloneVM);
            }
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

        private AsyncRelayCommand _addCredentialCommand;
        public AsyncRelayCommand AddCredentialCommand => _addCredentialCommand ??= new AsyncRelayCommand(AddCredentialAsync);

        private AsyncRelayCommand<CredentialViewModel> _editCredentialCommand;
        public AsyncRelayCommand<CredentialViewModel> EditCredentialCommand => _editCredentialCommand ??= new AsyncRelayCommand<CredentialViewModel>(EditCredentialAsync);

        private AsyncRelayCommand<CredentialViewModel> _deleteCredentialCommand;
        public AsyncRelayCommand<CredentialViewModel> DeleteCredentialCommand => _deleteCredentialCommand ??= new AsyncRelayCommand<CredentialViewModel>(DeleteCredentialAsync);
    }
}
