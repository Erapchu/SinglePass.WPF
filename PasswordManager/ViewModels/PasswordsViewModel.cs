using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Input;
using PasswordManager.Collections;
using PasswordManager.Enums;
using PasswordManager.Helpers;
using PasswordManager.Models;
using PasswordManager.Services;
using PasswordManager.Settings;
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
    public class PasswordsViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<PasswordsViewModel> _lazy = new(GetDesignTimeVM);
        public static PasswordsViewModel DesignTimeInstance => _lazy.Value;
        private static PasswordsViewModel GetDesignTimeVM()
        {
            var vm = new PasswordsViewModel();
            var cred = Credential.CreateNew();
            cred.NameField.Value = "Test";
            cred.LoginField.Value = "TestLogin";
            cred.PasswordField.Value = "TestPass";
            cred.OtherField.Value = "TestOther";
            var credVm = new CredentialViewModel(cred, null);
            vm.DisplayedCredentials.Add(credVm);
            return vm;
        }
        #endregion

        public event Action<CredentialViewModel> CredentialSelected;

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<PasswordsViewModel> _logger;
        private readonly List<CredentialViewModel> _credentials = new();
        private readonly AppSettingsService _appSettingsService;
        private readonly CredentialViewModelFactory _credentialViewModelFactory;

        private CredentialViewModel _selectedCredential;
        private string _searchText;
        private RelayCommand _addCredentialCommand;
        private RelayCommand<KeyEventArgs> _searchKeyEventCommand;

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
                _ = DisplayCredentialsAsync();
            }
        }

        private SortType _sort;
        public SortType Sort
        {
            get => _sort;
            set
            {
                SetProperty(ref _sort, value);
                _ = DisplayCredentialsAsync();
            }
        }

        private OrderType _order;
        public OrderType Order
        {
            get => _order;
            set
            {
                SetProperty(ref _order, value);
                _ = DisplayCredentialsAsync();
            }
        }

        private PasswordsViewModel() { }

        public PasswordsViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<PasswordsViewModel> logger,
            CredentialsDialogViewModel credentialsDialogViewModel,
            AppSettingsService appSettingsService,
            CredentialViewModelFactory credentialViewModelFactory)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _appSettingsService = appSettingsService;
            _credentialViewModelFactory = credentialViewModelFactory;

            _sort = _appSettingsService.Sort;
            _order = _appSettingsService.Order;

            ActiveCredentialDialogViewModel = credentialsDialogViewModel;
            ActiveCredentialDialogViewModel.Accept += ActiveCredentialDialogViewModel_Accept;
            ActiveCredentialDialogViewModel.Cancel += ActiveCredentialDialogViewModel_Cancel;
            ActiveCredentialDialogViewModel.Delete += ActiveCredentialDialogViewModel_Delete;
        }

        private async void ActiveCredentialDialogViewModel_Delete(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                PasswordManager.Language.Properties.Resources.DeleteItem,
                string.Format(PasswordManager.Language.Properties.Resources.Name0, credVM.NameFieldVM.Value),
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
                await DisplayCredentialsAsync();
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
            var dateTimeNow = DateTime.Now;
            newCredVM.LastModifiedTime = dateTimeNow;
            if (mode == CredentialsDialogMode.New)
            {
                newCredVM.CreationTime = dateTimeNow;
                await _credentialsCryptoService.AddCredential(newCredVM.Model);
                _credentials.Add(newCredVM);
                await DisplayCredentialsAsync();
            }
            else if (mode == CredentialsDialogMode.Edit)
            {
                await _credentialsCryptoService.EditCredential(newCredVM.Model);
                var staleCredVM = _credentials.FirstOrDefault(c => c.Model.Equals(newCredVM.Model));
                var staleIndex = _credentials.IndexOf(staleCredVM);
                _credentials.Remove(staleCredVM);
                _credentials.Insert(staleIndex, newCredVM);
                await DisplayCredentialsAsync();
            }

            SelectedCredential = newCredVM;
        }

        public void ReloadCredentials()
        {
            try
            {
                _credentials.Clear();

                var credentials = _credentialsCryptoService.Credentials;
                foreach (var cred in credentials)
                {
                    var credVM = _credentialViewModelFactory.ProvideNew(cred);
                    _credentials.Add(credVM);
                }

                _ = DisplayCredentialsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
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
                        tempList = _credentials.ToList();
                        SortAndOrder(tempList);
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
                        foreach (var cred in _credentials)
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

                        SortAndOrder(fCreds);
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
        }

        private void AddCredential()
        {
            ActiveCredentialDialogViewModel.CredentialViewModel = _credentialViewModelFactory.ProvideNew(Credential.CreateNew());
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

        private void SortAndOrder(List<CredentialViewModel> creds)
        {
            switch (Order)
            {
                case OrderType.Ascending:
                    {
                        switch (Sort)
                        {
                            case SortType.Name:
                                creds.Sort((a, b) => a.NameFieldVM.Value.CompareTo(b.NameFieldVM.Value));
                                break;
                            case SortType.Created:
                                creds.Sort((a, b) => a.CreationTime.CompareTo(b.CreationTime));
                                break;
                            case SortType.Modified:
                                creds.Sort((a, b) => a.LastModifiedTime.CompareTo(b.LastModifiedTime));
                                break;
                        }
                    }
                    break;
                case OrderType.Descending:
                    {
                        switch (Sort)
                        {
                            case SortType.Name:
                                creds.Sort((a, b) => b.NameFieldVM.Value.CompareTo(a.NameFieldVM.Value));
                                break;
                            case SortType.Created:
                                creds.Sort((a, b) => b.CreationTime.CompareTo(a.CreationTime));
                                break;
                            case SortType.Modified:
                                creds.Sort((a, b) => b.LastModifiedTime.CompareTo(a.LastModifiedTime));
                                break;
                        }
                    }
                    break;
            }
        }
    }
}
