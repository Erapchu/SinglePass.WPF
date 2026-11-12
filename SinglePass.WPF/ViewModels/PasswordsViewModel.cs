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
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

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
        private Task _initialLoad;

        [ObservableProperty]
        public partial bool HasLoadedCredentials { get; private set; }

        public event Action<CredentialViewModel> CredentialSelected;
        public event Action<CredentialViewModel> ScrollIntoViewRequired;
        public event Action<string> StatusMessageRequested;

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();
        public ListCollectionView GroupedCredentials { get; private set; }

        [ObservableProperty]
        public partial CredentialViewModel SelectedCredentialVM { get; set; }

        partial void OnSelectedCredentialVMChanged(CredentialViewModel value) => CredentialSelected?.Invoke(value);

        [ObservableProperty]
        public partial string SearchText { get; set; }

        partial void OnSearchTextChanged(string value) => _ = DisplayCredentialsAsync();

        private SortType _sort;
        public SortType Sort => _sort;

        private OrderType _order;
        public OrderType Order => _order;

        public GridLength PasswordListPaneWidth { get; set; } = new(1, GridUnitType.Star);
        public GridLength DetailsPaneWidth { get; set; } = new(1, GridUnitType.Star);
        public double DetailsPaneRatio => DetailsPaneWidth.Value / (PasswordListPaneWidth.Value + DetailsPaneWidth.Value);

        private int _displayRequest;

        public bool SortByName => Sort == SortType.Name;
        public bool SortByCreated => Sort == SortType.Created;
        public bool SortByModified => Sort == SortType.Modified;
        public bool IsAscending => Order == OrderType.Ascending;
        public bool IsDescending => Order == OrderType.Descending;

        [RelayCommand]
        private async Task SortByAsync(SortType sort)
        {
            if (Sort == sort)
                return;

            _sort = sort;
            OnPropertyChanged(nameof(Sort));
            OnPropertyChanged(nameof(SortByName));
            OnPropertyChanged(nameof(SortByCreated));
            OnPropertyChanged(nameof(SortByModified));
            await DisplayCredentialsAsync(keepSelection: true);
        }

        [RelayCommand]
        private async Task OrderByAsync(OrderType order)
        {
            if (Order == order)
                return;

            _order = order;
            OnPropertyChanged(nameof(Order));
            OnPropertyChanged(nameof(IsAscending));
            OnPropertyChanged(nameof(IsDescending));
            await DisplayCredentialsAsync(keepSelection: true);
        }

        [ObservableProperty]
        public partial bool IsPasswordVisible { get; set; }

        private PasswordsViewModel()
        {
            GroupedCredentials = CreateGroupedView(DisplayedCredentials, SortType.Name);
        }

        public PasswordsViewModel(
            CredentialsCryptoService credentialsCryptoService,
            ILogger<PasswordsViewModel> logger,
            AppSettingsService appSettingsService,
            CredentialViewModelFactory credentialViewModelFactory) : this()
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _appSettingsService = appSettingsService;
            _credentialViewModelFactory = credentialViewModelFactory;

            _sort = _appSettingsService.Sort is SortType.Created or SortType.Modified
                ? _appSettingsService.Sort
                : SortType.Name;
            _order = _appSettingsService.Order == OrderType.Descending ? OrderType.Descending : OrderType.Ascending;

            var detailsPaneRatio = _appSettingsService.DetailsPaneRatio;
            if (detailsPaneRatio is > 0 and < 1)
            {
                PasswordListPaneWidth = new GridLength(1 - detailsPaneRatio, GridUnitType.Star);
                DetailsPaneWidth = new GridLength(detailsPaneRatio, GridUnitType.Star);
            }
        }

        public Task LoadInitialCredentialsAsync() => _initialLoad ??= LoadInitialCredentialsCoreAsync();

        private async Task LoadInitialCredentialsCoreAsync()
        {
            try
            {
                // Let WPF render the window before publishing the initial list.
                await Dispatcher.Yield(DispatcherPriority.Background);
                var credentials = await Task.Run(_credentialViewModelFactory.ProvideAllNew);
                _credentialVMs.AddRange(credentials);
                await DisplayCredentialsAsync();
                HasLoadedCredentials = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        public async Task ReloadCredentialsAsync()
        {
            try
            {
                if (_initialLoad is not null)
                    await _initialLoad;

                _credentialVMs.Clear();
                _credentialVMs.AddRange(_credentialViewModelFactory.ProvideAllNew());

                await DisplayCredentialsAsync();
                HasLoadedCredentials = true;
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

                    return SortAndOrder(credentials, sort, order);
                });

                if (request != _displayRequest)
                    return;

                var selection = keepSelection ? SelectedCredentialVM : null;
                DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(filtered);
                GroupedCredentials = CreateGroupedView(DisplayedCredentials, sort);
                OnPropertyChanged(nameof(DisplayedCredentials));
                OnPropertyChanged(nameof(GroupedCredentials));
                var newSelection = selection != null && filtered.Contains(selection)
                    ? selection
                    : DisplayedCredentials.FirstOrDefault();
                if (ReferenceEquals(SelectedCredentialVM, newSelection))
                    CredentialSelected?.Invoke(newSelection);
                else
                    SelectedCredentialVM = newSelection;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private async Task Add()
        {
            if (_initialLoad is not null)
                await _initialLoad;

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

        private static List<CredentialViewModel> SortAndOrder(List<CredentialViewModel> credentials, SortType sort, OrderType order)
        {
            var sorted = sort switch
            {
                SortType.Created => credentials.OrderBy(item => item.CreationTime),
                SortType.Modified => credentials.OrderBy(item => item.LastModifiedTime),
                _ => credentials.OrderBy(GetInitial, StringComparer.CurrentCulture)
                    .ThenBy(GetInitial, StringComparer.Ordinal)
                    .ThenBy(item => item.NameFieldVM.Value, StringComparer.CurrentCulture)
            };

            var result = sorted.ToList();
            if (order == OrderType.Descending)
                result.Reverse();

            return result;
        }

        private static ListCollectionView CreateGroupedView(ObservableCollectionDelayed<CredentialViewModel> credentials, SortType sort)
        {
            var view = new ListCollectionView(credentials) { IsDataInGroupOrder = true };
            view.GroupDescriptions.Add(new CredentialGroupDescription(sort));
            return view;
        }

        private static string GetInitial(CredentialViewModel credential)
        {
            var name = credential.NameFieldVM.Value?.TrimStart();
            return !string.IsNullOrEmpty(name) && char.IsLetter(name, 0)
                ? StringInfo.GetNextTextElement(name).ToUpperInvariant().Normalize()
                : "#";
        }

        private sealed class CredentialGroupDescription(SortType sort) : GroupDescription
        {
            public override object GroupNameFromItem(object item, int level, CultureInfo culture)
            {
                var credential = (CredentialViewModel)item;
                if (sort == SortType.Name)
                    return GetInitial(credential);

                var date = sort == SortType.Created ? credential.CreationTime : credential.LastModifiedTime;
                return date == default ? null : new DateTime(date.Year, date.Month, 1);
            }
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
