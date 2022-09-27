using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Collections;
using SinglePass.WPF.Enums;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.Views.MessageBox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;
using Unidecode.NET;

namespace SinglePass.WPF.ViewModels
{
    [INotifyPropertyChanged]
    public partial class PasswordsViewModel
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

        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly ILogger<PasswordsViewModel> _logger;
        private readonly List<CredentialViewModel> _credentialVMs = new();
        private readonly AppSettingsService _appSettingsService;
        private readonly CredentialViewModelFactory _credentialViewModelFactory;

        public event Action<CredentialViewModel> CredentialSelected;
        public event Action<CredentialViewModel> ScrollIntoViewRequired;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();
        public CredentialsDialogViewModel ActiveCredentialDialogVM { get; }

        private CredentialViewModel _selectedCredential;
        public CredentialViewModel SelectedCredential
        {
            get => _selectedCredential;
            set
            {
                SetProperty(ref _selectedCredential, value);
                ActiveCredentialDialogVM.Mode = CredentialsDialogMode.View;
                ActiveCredentialDialogVM.CredentialViewModel = value;
                ActiveCredentialDialogVM.IsPasswordVisible = false;
                CredentialSelected?.Invoke(value);
            }
        }

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

            ActiveCredentialDialogVM = credentialsDialogViewModel;
            ActiveCredentialDialogVM.Accept += ActiveCredentialDialogVM_Accept;
            ActiveCredentialDialogVM.Cancel += ActiveCredentialDialogVM_Cancel;
            ActiveCredentialDialogVM.Delete += ActiveCredentialDialogVM_Delete;
        }

        private async void ActiveCredentialDialogVM_Delete(CredentialViewModel credVM)
        {
            var result = await MaterialMessageBox.ShowAsync(
                SinglePass.Language.Properties.Resources.DeleteItem,
                string.Format(SinglePass.Language.Properties.Resources.Name0, credVM.NameFieldVM.Value),
                MaterialMessageBoxButtons.YesNo,
                DialogIdentifiers.MainWindowName,
                PackIconKind.Delete);
            if (result == MaterialDialogResult.Yes)
            {
                await _credentialsCryptoService.DeleteCredential(credVM.Model);
                _credentialVMs.Remove(credVM);
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

        private void ActiveCredentialDialogVM_Cancel()
        {
            ActiveCredentialDialogVM.IsPasswordVisible = false;
            ActiveCredentialDialogVM.Mode = CredentialsDialogMode.View;
            ActiveCredentialDialogVM.CredentialViewModel = SelectedCredential;
        }

        private async void ActiveCredentialDialogVM_Accept(CredentialViewModel newCredVM, CredentialsDialogMode mode)
        {
            var dateTimeNow = DateTime.Now;
            newCredVM.LastModifiedTime = dateTimeNow;
            if (mode == CredentialsDialogMode.New)
            {
                newCredVM.CreationTime = dateTimeNow;
                await _credentialsCryptoService.AddCredential(newCredVM.Model);
                _credentialVMs.Add(newCredVM);
                await DisplayCredentialsAsync();
            }
            else if (mode == CredentialsDialogMode.Edit)
            {
                await _credentialsCryptoService.EditCredential(newCredVM.Model);
                var staleCredVM = _credentialVMs.FirstOrDefault(c => c.Model.Equals(newCredVM.Model));
                var staleIndex = _credentialVMs.IndexOf(staleCredVM);
                _credentialVMs.Remove(staleCredVM);
                _credentialVMs.Insert(staleIndex, newCredVM);
                await DisplayCredentialsAsync();
            }

            SelectedCredential = newCredVM;
        }

        public void ReloadCredentials()
        {
            try
            {
                _credentialVMs.Clear();
                _credentialVMs.AddRange(_credentialViewModelFactory.ProvideAllNew());

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
                        tempList = _credentialVMs.ToList();
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

        [RelayCommand]
        private void AddCredential()
        {
            ActiveCredentialDialogVM.CredentialViewModel = _credentialViewModelFactory.ProvideNew(Credential.CreateNew());
            ActiveCredentialDialogVM.Mode = CredentialsDialogMode.New;
            ActiveCredentialDialogVM.IsPasswordVisible = true;
            ActiveCredentialDialogVM.SetFocus();
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
                        var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredential);
                        if (selectedIndex != -1 && selectedIndex > 0)
                        {
                            SelectedCredential = DisplayedCredentials[selectedIndex - 1];
                            ScrollIntoViewRequired?.Invoke(SelectedCredential);
                        }
                        break;
                    }
                case Key.Down:
                    {
                        // Select next
                        var selectedIndex = DisplayedCredentials.IndexOf(SelectedCredential);
                        if (selectedIndex != -1 && selectedIndex < DisplayedCredentials.Count - 1)
                        {
                            SelectedCredential = DisplayedCredentials[selectedIndex + 1];
                            ScrollIntoViewRequired?.Invoke(SelectedCredential);
                        }
                        break;
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
