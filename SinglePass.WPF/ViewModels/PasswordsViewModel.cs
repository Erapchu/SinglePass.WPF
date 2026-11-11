using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
using System.Threading.Tasks;
using System.Windows.Input;

namespace SinglePass.WPF.ViewModels
{
    public partial class PasswordsViewModel : ObservableObject
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
        public event Action<string> StatusMessageRequested;

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
        public SortType Sort => _sort;

        private OrderType _order;
        public OrderType Order => _order;

        private int _displayRequest;

        public string NameSortIndicator => GetSortIndicator(SortType.Name);
        public string WebsiteSortIndicator => GetSortIndicator(SortType.Website);
        public string ModifiedSortIndicator => GetSortIndicator(SortType.Modified);

        private string GetSortIndicator(SortType column) =>
            Sort == column ? (Order == OrderType.Ascending ? "↑" : "↓") : string.Empty;

        [RelayCommand]
        private void SortBy(SortType column)
        {
            var order = Sort == column && Order == OrderType.Ascending
                ? OrderType.Descending
                : OrderType.Ascending;
            _sort = column;
            _order = order;
            OnPropertyChanged(nameof(NameSortIndicator));
            OnPropertyChanged(nameof(WebsiteSortIndicator));
            OnPropertyChanged(nameof(ModifiedSortIndicator));
            _ = DisplayCredentialsAsync(keepSelection: true);
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

        private async Task DisplayCredentialsAsync(bool keepSelection = false)
        {
            var request = ++_displayRequest;
            try
            {
                var filterText = SearchText;
                var sort = Sort;
                var order = Order;
                var credentials = _credentialVMs.ToList();
                var filtered = await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(filterText))
                    {
                        var search = new CredentialSearch(filterText);
                        credentials.RemoveAll(item => !search.Matches(item));
                    }

                    SortAndOrder(credentials, sort, order);
                    return credentials;
                });

                if (request != _displayRequest)
                    return;

                var selection = keepSelection ? SelectedCredentialVM : null;
                DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(filtered);
                OnPropertyChanged(nameof(DisplayedCredentials));
                SelectedCredentialVM = selection != null && filtered.Contains(selection)
                    ? selection
                    : DisplayedCredentials.FirstOrDefault();
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
            var result = CredentialEditDialog.ShowDialog(
                newCredVM,
                CredentialDetailsMode.New);
            if (result != DialogChoice.OK)
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

        private static void SortAndOrder(List<CredentialViewModel> credentials, SortType sort, OrderType order)
        {
            Comparison<CredentialViewModel> compare = sort switch
            {
                SortType.Website => (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.SiteFieldVM.Value, b.SiteFieldVM.Value),
                SortType.Created => (a, b) => a.CreationTime.CompareTo(b.CreationTime),
                SortType.Modified => (a, b) => a.LastModifiedTime.CompareTo(b.LastModifiedTime),
                _ => (a, b) => StringComparer.CurrentCulture.Compare(a.NameFieldVM.Value, b.NameFieldVM.Value)
            };

            if (order == OrderType.Descending)
                credentials.Sort((a, b) => compare(b, a));
            else
                credentials.Sort(compare);
        }

        [RelayCommand]
        private async Task Edit()
        {
            if (SelectedCredentialVM is null)
                return;

            var tempCredentialVM = SelectedCredentialVM.Clone();
            var result = CredentialEditDialog.ShowDialog(
                tempCredentialVM,
                CredentialDetailsMode.Edit);
            if (result != DialogChoice.OK)
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

            var result = MessageDialog.ShowDialog(
                SinglePass.Language.Properties.Resources.DeleteItem,
                string.Format(SinglePass.Language.Properties.Resources.Name0, SelectedCredentialVM.NameFieldVM.Value),
                MessageDialogButtons.YesNo,
                "\uE74D");

            if (result == DialogChoice.Yes)
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
                StatusMessageRequested?.Invoke(SinglePass.Language.Properties.Resources.TextCopied);
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
