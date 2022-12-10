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
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public event Action<string> EnqueueSnackbarMessage;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();

        private CredentialViewModel _selectedCredentialVM;
        public CredentialViewModel SelectedCredentialVM
        {
            get => _selectedCredentialVM;
            set
            {
                SetProperty(ref _selectedCredentialVM, value);
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

        [ObservableProperty]
        private bool _isPasswordVisible;

        private PasswordsViewModel() { }

        public PasswordsViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<PasswordsViewModel> logger,
            AppSettingsService appSettingsService,
            CredentialViewModelFactory credentialViewModelFactory)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _appSettingsService = appSettingsService;
            _credentialViewModelFactory = credentialViewModelFactory;

            _sort = _appSettingsService.Sort;
            _order = _appSettingsService.Order;
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
                SelectedCredentialVM = DisplayedCredentials.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private async Task Add()
        {
            var newCredVM = _credentialViewModelFactory.ProvideNew(Credential.CreateNew());
            var result = CredentialDialog.ShowDialog(
                newCredVM,
                CredentialDetailsMode.New);
            if (result != MaterialDialogResult.OK)
                return;

            var dateTimeNow = DateTime.Now;
            newCredVM.CreationTime = dateTimeNow;
            newCredVM.LastModifiedTime = dateTimeNow;
            await _credentialsCryptoService.AddCredential(newCredVM.Model);
            _credentialVMs.Add(newCredVM);
            await DisplayCredentialsAsync();
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

        [RelayCommand]
        private async Task Edit()
        {
            if (SelectedCredentialVM is null)
                return;

            var tempCredentialVM = SelectedCredentialVM.Clone();
            var result = CredentialDialog.ShowDialog(
                tempCredentialVM,
                CredentialDetailsMode.Edit);
            if (result != MaterialDialogResult.OK)
                return;

            var dateTimeNow = DateTime.Now;
            tempCredentialVM.LastModifiedTime = dateTimeNow;
            await _credentialsCryptoService.EditCredential(tempCredentialVM.Model);
            _credentialVMs.Remove(SelectedCredentialVM);
            _credentialVMs.Add(tempCredentialVM);
            await DisplayCredentialsAsync();
            SelectedCredentialVM = tempCredentialVM;
        }

        [RelayCommand]
        private async Task Delete()
        {
            if (SelectedCredentialVM is null)
                return;

            var result = MaterialMessageBox.ShowDialog(
                SinglePass.Language.Properties.Resources.DeleteItem,
                string.Format(SinglePass.Language.Properties.Resources.Name0, SelectedCredentialVM.NameFieldVM.Value),
                MaterialMessageBoxButtons.YesNo,
                PackIconKind.Delete);

            if (result == MaterialDialogResult.Yes)
            {
                await _credentialsCryptoService.DeleteCredential(SelectedCredentialVM.Model);
                _credentialVMs.Remove(SelectedCredentialVM);
                var dIndex = DisplayedCredentials.IndexOf(SelectedCredentialVM);
                var countAfterDeletion = DisplayedCredentials.Count - 1;
                var sIndex = dIndex >= countAfterDeletion ? countAfterDeletion - 1 : dIndex;
                await DisplayCredentialsAsync();
                if (sIndex >= 0)
                {
                    SelectedCredentialVM = DisplayedCredentials.ElementAt(sIndex);
                }
            }
        }

        [RelayCommand]
        private void CopyToClipboard(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            try
            {
                WindowsClipboard.SetText(data);
                EnqueueSnackbarMessage?.Invoke(SinglePass.Language.Properties.Resources.TextCopied);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private void OpenInBrowser()
        {
            var uri = SelectedCredentialVM?.SiteFieldVM?.Value;
            if (string.IsNullOrWhiteSpace(uri))
                return;

            uri = uri.Replace("&", "^&");
            if (Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out var site))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {site}") { CreateNoWindow = true });
            }
        }
    }
}
