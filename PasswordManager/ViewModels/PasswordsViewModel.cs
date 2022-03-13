using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Enums;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Views.MessageBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Unidecode.NET;

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
            vm.DisplayedCredentials.Add(credVm);
            return vm;
        }
        #endregion

        public event Action<CredentialViewModel> CredentialSelected;

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly SyncService _syncService;
        private readonly ILogger<PasswordsViewModel> _logger;
        private readonly List<CredentialViewModel> _credentials = new();
        private CredentialViewModel _selectedCredential;
        private string _searchText;
        private bool _searchTextFocused;
        private RelayCommand _addCredentialCommand;
        private RelayCommand<KeyEventArgs> _searchKeyEventCommand;
        private bool _cloudSyncInProgress;

        public RelayCommand AddCredentialCommand => _addCredentialCommand ??= new RelayCommand(AddCredential);
        public RelayCommand<KeyEventArgs> SearchKeyEventCommand => _searchKeyEventCommand ??= new RelayCommand<KeyEventArgs>(HandleSearchKeyEvent);
        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();
        public CredentialsDialogViewModel ActiveCredentialDialogViewModel { get; }

        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                SetProperty(ref _selectedCredential, value);
                ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.View;
                ActiveCredentialDialogViewModel.CredentialViewModel = value;
                ActiveCredentialDialogViewModel.IsPasswordVisible = false;
                CredentialSelected?.Invoke(value);
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                _ = FilterCredentialsAsync();
            }
        }

        public bool SearchTextFocused
        {
            get => _searchTextFocused;
            set => SetProperty(ref _searchTextFocused, value);
        }

        public bool CloudSyncInProgress
        {
            get => _cloudSyncInProgress;
            set => SetProperty(ref _cloudSyncInProgress, value);
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(
            CredentialsCryptoService credentialsCryptoService,
            SyncService syncService,
            ILogger<PasswordsViewModel> logger,
            CredentialsDialogViewModel credentialsDialogViewModel)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _syncService = syncService;
            _logger = logger;

            _syncService.SyncReport += SyncService_SyncReport;

            Name = "Credentials";
            ItemIndex = PasswordsNavigationItemIndex;
            IconKind = PackIconKind.Password;
            ActiveCredentialDialogViewModel = credentialsDialogViewModel;
            ActiveCredentialDialogViewModel.Accept += ActiveCredentialDialogViewModel_Accept;
            ActiveCredentialDialogViewModel.Cancel += ActiveCredentialDialogViewModel_Cancel;
            ActiveCredentialDialogViewModel.Delete += ActiveCredentialDialogViewModel_Delete;
        }

        private void SyncService_SyncReport(bool obj)
        {
            CloudSyncInProgress = obj;
        }

        private async void ActiveCredentialDialogViewModel_Delete(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                "Delete credential?",
                $"Name: {credVM.NameFieldVM.Value}",
                MaterialMessageBoxButtons.YesNo,
                MvvmHelper.MainWindowDialogName,
                PackIconKind.Delete);
            if (result == MaterialDialogResult.Yes)
            {
                await _credentialsCryptoService.DeleteCredential(credVM.Model);
                _credentials.Remove(credVM);
                var dIndex = DisplayedCredentials.IndexOf(credVM);
                var countAfterDeletion = DisplayedCredentials.Count - 1;
                var sIndex = dIndex >= countAfterDeletion ? countAfterDeletion - 1 : dIndex;
                await FilterCredentialsAsync();
                if (sIndex >= 0)
                {
                    SelectedCredential = DisplayedCredentials.ElementAt(sIndex);
                }
            }
        }

        private void ActiveCredentialDialogViewModel_Cancel()
        {
            ActiveCredentialDialogViewModel.IsPasswordVisible = false;
            ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.View;
            ActiveCredentialDialogViewModel.CredentialViewModel = SelectedCredential;
        }

        private async void ActiveCredentialDialogViewModel_Accept(CredentialViewModel newCredVM, CredentialsDialogMode mode)
        {
            try
            {
                Loading = true;

                newCredVM.LastModifiedTime = DateTime.Now;
                if (mode == CredentialsDialogMode.New)
                {
                    await _credentialsCryptoService.AddCredential(newCredVM.Model);
                    _credentials.Add(newCredVM);
                    await FilterCredentialsAsync();
                }
                else if (mode == CredentialsDialogMode.Edit)
                {
                    await _credentialsCryptoService.EditCredential(newCredVM.Model);
                    var staleCredVM = _credentials.FirstOrDefault(c => c.Model.Equals(newCredVM.Model));
                    var staleIndex = _credentials.IndexOf(staleCredVM);
                    _credentials.Remove(staleCredVM);
                    _credentials.Insert(staleIndex, newCredVM);
                    await FilterCredentialsAsync();
                }

                SelectedCredential = newCredVM;
            }
            finally
            {
                Loading = false;
            }
        }

        public void LoadCredentials()
        {
            try
            {
                Loading = true;
                var credentials = _credentialsCryptoService.Credentials;
                using var delayed = DisplayedCredentials.DelayNotifications();
                foreach (var cred in credentials)
                {
                    var credVM = new CredentialViewModel(cred);
                    _credentials.Add(credVM);
                    delayed.Add(credVM);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }
        }

        public async Task FilterCredentialsAsync()
        {
            try
            {
                Loading = true;
                List<CredentialViewModel> filteredCredentials = null;
                var filterText = SearchText;

                if (string.IsNullOrEmpty(filterText))
                {
                    filteredCredentials = _credentials;
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
                        foreach (var cred in _credentials)
                        {
                            var nameValue = cred.NameFieldVM.Value;
                            if (nameValue.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) != -1
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
                SelectedCredential = DisplayedCredentials.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
            finally
            {
                Loading = false;
            }
        }

        private void AddCredential()
        {
            ActiveCredentialDialogViewModel.CredentialViewModel = new CredentialViewModel(new Credential());
            ActiveCredentialDialogViewModel.Mode = CredentialsDialogMode.New;
            ActiveCredentialDialogViewModel.IsPasswordVisible = true;
            ActiveCredentialDialogViewModel.SetFocus();
        }

        private void HandleSearchKeyEvent(KeyEventArgs args)
        {
            if (args is null)
                return;

            if (args.Key == Key.Up)
            {
                // Select previous
                var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredential);
                if (selectedIndex != -1 && selectedIndex > 0)
                {
                    SelectedCredential = DisplayedCredentials[selectedIndex - 1];
                }
            }

            if (args.Key == Key.Down)
            {
                // Select next
                var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredential);
                if (selectedIndex != -1 && selectedIndex < DisplayedCredentials.Count - 1)
                {
                    SelectedCredential = DisplayedCredentials[selectedIndex + 1];
                }
            }
        }
    }
}
