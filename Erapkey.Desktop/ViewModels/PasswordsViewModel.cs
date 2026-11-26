using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Erapkey.Desktop.Collections;
using Erapkey.Desktop.Enums;
using Erapkey.Desktop.Helpers;
using Erapkey.Core.Localization;
using Erapkey.Core.Models;
using Erapkey.Desktop.Services;
using Erapkey.Desktop.Settings;
using Erapkey.Desktop.ViewModels.Dialogs;
using Erapkey.Desktop.ViewModels.Entries;
using Erapkey.Desktop.Views.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Erapkey.Core.Services;

namespace Erapkey.Desktop.ViewModels
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
            cred.Login.LoginField.Value = "TestLogin";
            cred.Login.PasswordField.Value = "TestPass";
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
        private readonly SshAgentServer _sshAgent;
        private Task _initialLoad;
        private Guid? _selectedGroupId;

        /// <summary>
        /// Entry type the list is limited to (a category in the sidebar), or null for all types.
        /// The sidebar selects either a category or a group, never both.
        /// </summary>
        public CredentialType? SelectedCategory { get; private set; }

        public IReadOnlyList<PasswordGroup> PasswordGroups => _credentialsCryptoService?.PasswordGroups ?? [];

        internal bool IsSavingGroups { get; private set; }

        [ObservableProperty]
        public partial bool HasLoadedCredentials { get; private set; }

        public event Action<CredentialViewModel> CredentialSelected;
        public event Action<CredentialViewModel> ScrollIntoViewRequired;
        public event Action<string> StatusMessageRequested;

        internal void ShowStatus(string message) => StatusMessageRequested?.Invoke(message);

        public ObservableCollectionDelayed<CredentialViewModel> DisplayedCredentials { get; private set; } = new();
        public ListCollectionView GroupedCredentials { get; private set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditCredential))]
        public partial CredentialViewModel SelectedCredentialVM { get; set; }

        partial void OnSelectedCredentialVMChanged(CredentialViewModel value)
        {
            DraftCredential = null;
            OnPropertyChanged(nameof(CurrentGroup));
            EditCommand.NotifyCanExecuteChanged();
            CredentialSelected?.Invoke(value);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEditing))]
        [NotifyPropertyChangedFor(nameof(IsAdding))]
        [NotifyPropertyChangedFor(nameof(CanEditCredential))]
        [NotifyCanExecuteChangedFor(nameof(EditCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
        public partial CredentialViewModel DraftCredential { get; private set; }

        public bool IsEditing => DraftCredential != null;
        public bool IsAdding => IsEditing && _isNewDraft;

        private bool _isNewDraft;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddCommand))]
        [NotifyPropertyChangedFor(nameof(CanEditCredential))]
        [NotifyCanExecuteChangedFor(nameof(EditCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(CancelEditCommand))]
        [NotifyCanExecuteChangedFor(nameof(SaveEditCommand))]
        public partial bool IsSavingEdit { get; private set; }

        public sealed record GroupOption(Guid? Id, string Name, string Icon)
        {
            public bool IsUngrouped => !Id.HasValue;
        }

        private static readonly GroupOption Ungrouped = new(null, null, "\uE8B7");

        public System.Collections.ObjectModel.ObservableCollection<GroupOption> GroupOptions { get; } = [Ungrouped];

        private Guid? _originalGroupId;
        private Guid? _draftGroupId;

        public GroupOption CurrentGroup => FindGroupOption(SelectedCredentialVM is { } credential
            ? _credentialsCryptoService?.GetGroupId(credential.Model.Id)
            : null);

        public GroupOption DraftGroup
        {
            get => FindGroupOption(_draftGroupId);
            set
            {
                if (value == null || _draftGroupId == value.Id)
                    return;

                _draftGroupId = value.Id;
                OnPropertyChanged();
            }
        }

        private GroupOption FindGroupOption(Guid? id) => GroupOptions.FirstOrDefault(option => option.Id == id) ?? Ungrouped;

        private void RefreshGroupOptions()
        {
            var options = PasswordGroups.Select(ToGroupOption).ToHashSet();
            for (var index = GroupOptions.Count - 1; index > 0; index--)
                if (!options.Remove(GroupOptions[index]))
                    GroupOptions.RemoveAt(index);

            foreach (var option in options)
                GroupOptions.Add(option);

            if (_draftGroupId.HasValue && FindGroupOption(_draftGroupId).IsUngrouped)
                _draftGroupId = _originalGroupId;

            OnPropertyChanged(nameof(CurrentGroup));
            OnPropertyChanged(nameof(DraftGroup));
        }

        private static GroupOption ToGroupOption(PasswordGroup group) => new(group.Id, group.Name, group.Icon);

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
            CredentialViewModelFactory credentialViewModelFactory,
            SshAgentServer sshAgent) : this()
        {
            _credentialsCryptoService = credentialsCryptoService;
            _logger = logger;
            _appSettingsService = appSettingsService;
            _credentialViewModelFactory = credentialViewModelFactory;
            _sshAgent = sshAgent;
            RefreshGroupOptions();

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

        /// <summary>Limits the list to a group or to an entry type; both null shows every entry.</summary>
        internal void SelectFilter(Guid? groupId, CredentialType? category)
        {
            if (_selectedGroupId == groupId && SelectedCategory == category)
                return;

            _selectedGroupId = groupId;
            SelectedCategory = category;
            OnPropertyChanged(nameof(SelectedCategory));
            _ = DisplayCredentialsAsync(keepSelection: true);
        }

        internal Task<bool> AddGroupAsync(PasswordGroup group) =>
            SaveGroupsAsync(() => _credentialsCryptoService.SaveGroup(group, isNew: true));

        internal Task<bool> EditGroupAsync(PasswordGroup group) =>
            SaveGroupsAsync(() => _credentialsCryptoService.SaveGroup(group, isNew: false));

        internal Task<bool> DeleteGroupAsync(PasswordGroup group) =>
            SaveGroupsAsync(() => _credentialsCryptoService.DeleteGroup(group.Id));

        internal bool CanMoveToGroup(IReadOnlyCollection<Guid> credentialIds, Guid? groupId) =>
            !IsSavingGroups &&
            (groupId == null || PasswordGroups.Any(group => group.Id == groupId)) &&
            credentialIds.Any(id => _credentialsCryptoService.GetGroupId(id) != groupId);

        internal Task MoveToGroupAsync(Guid credentialId, Guid? groupId) => MoveToGroupAsync([credentialId], groupId);

        internal async Task MoveToGroupAsync(IReadOnlyCollection<Guid> credentialIds, Guid? groupId)
        {
            if (!CanMoveToGroup(credentialIds, groupId))
                return;

            if (await SaveGroupsAsync(() => _credentialsCryptoService.MoveCredentials(credentialIds, groupId)) && _selectedGroupId.HasValue)
                await DisplayCredentialsAsync(keepSelection: true);
        }

        private async Task<bool> SaveGroupsAsync(Func<Task> save)
        {
            if (IsSavingGroups)
                return false;

            IsSavingGroups = true;
            try
            {
                await save();
                RefreshGroupOptions();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save password groups.");
                StatusMessageRequested?.Invoke(LocalizationManager.Instance["SavePasswordGroupsFailed"]);
                return false;
            }
            finally
            {
                IsSavingGroups = false;
            }
        }

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
                RefreshGroupOptions();

                await DisplayCredentialsAsync();
                HasLoadedCredentials = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        private async Task DisplayCredentialsAsync(bool keepSelection = false, CredentialViewModel preferredSelection = null)
        {
            var request = ++_displayRequest;
            try
            {
                var filterText = SearchText;
                var sort = Sort;
                var order = Order;
                var credentials = _credentialVMs.ToList();
                var groupId = _selectedGroupId;
                var category = SelectedCategory;
                var assignments = _credentialsCryptoService?.GroupAssignments;
                var filtered = await Task.Run(() =>
                {
                    if (groupId.HasValue)
                        credentials.RemoveAll(item => !assignments.TryGetValue(item.Model.Id, out var assignedGroupId) || assignedGroupId != groupId);
                    if (category.HasValue)
                        credentials.RemoveAll(item => item.Type != category);

                    if (!string.IsNullOrEmpty(filterText))
                    {
                        var search = new CredentialSearch(filterText);
                        credentials.RemoveAll(item => !search.Matches(item));
                    }

                    return SortAndOrder(credentials, sort, order);
                });

                if (request != _displayRequest)
                    return;

                RefreshSshKeyOptions();
                var selection = preferredSelection ?? (keepSelection ? SelectedCredentialVM : null);
                DisplayedCredentials = new ObservableCollectionDelayed<CredentialViewModel>(filtered);
                GroupedCredentials = CreateGroupedView(DisplayedCredentials, sort);
                OnPropertyChanged(nameof(DisplayedCredentials));
                OnPropertyChanged(nameof(GroupedCredentials));
                var newSelection = (selection == null ? null : filtered.Find(item => item.Model.Id == selection.Model.Id))
                    ?? DisplayedCredentials.FirstOrDefault();
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

        private bool CanAdd() => !IsSavingEdit;

        [RelayCommand(CanExecute = nameof(CanAdd))]
        private async Task Add(CredentialType type, CancellationToken cancellationToken)
        {
            if (_initialLoad is not null)
                await _initialLoad;

            if (cancellationToken.IsCancellationRequested || !CanAdd())
                return;

            DraftCredential = null;
            StartDraft(_credentialViewModelFactory.ProvideNew(Credential.CreateNew(type)), _selectedGroupId, isNew: true);
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

        public bool CanEditCredential => SelectedCredentialVM != null && !IsEditing && !IsSavingEdit;

        [RelayCommand(CanExecute = nameof(CanEditCredential))]
        private void Edit()
        {
            if (SelectedCredentialVM is not { } credential)
                return;

            StartDraft(credential.Clone(), CurrentGroup.Id, isNew: false);
        }

        private void StartDraft(CredentialViewModel draft, Guid? groupId, bool isNew)
        {
            _isNewDraft = isNew;
            _originalGroupId = isNew ? null : groupId;
            _draftGroupId = groupId;
            OnPropertyChanged(nameof(DraftGroup));
            DraftCredential = draft;
        }

        private bool CanFinishEdit() => IsEditing && !IsSavingEdit;

        // Saving needs a name; the Save button stays disabled until one is entered.
        private bool CanSaveEdit() => CanFinishEdit() && DraftCredential.NameFieldVM.HasText;

        partial void OnDraftCredentialChanged(CredentialViewModel oldValue, CredentialViewModel newValue)
        {
            if (oldValue != null)
                oldValue.NameFieldVM.PropertyChanged -= DraftName_PropertyChanged;
            if (newValue != null)
                newValue.NameFieldVM.PropertyChanged += DraftName_PropertyChanged;
        }

        private void DraftName_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PassFieldViewModel.HasText))
                SaveEditCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanFinishEdit))]
        private void CancelEdit() => DraftCredential = null;

        [RelayCommand(CanExecute = nameof(CanSaveEdit))]
        private async Task SaveEdit()
        {
            if (DraftCredential is not { } draft)
                return;

            draft.NameFieldVM.ValidateValue();
            if (draft.NameFieldVM.HasErrors)
                return;

            var login = draft.Login;
            var originalTotp = login?.Model.Totp;
            TotpConfiguration totp = null;
            if (login != null && !TryGetEditedTotp(login, out totp))
            {
                login.HasInvalidTotp = true;
                return;
            }

            var groupId = _draftGroupId;
            var changeGroup = _draftGroupId != _originalGroupId;
            var isNew = IsAdding;
            IsSavingEdit = true;
            try
            {
                draft.LastModifiedTime = DateTime.UtcNow;
                if (login != null)
                    login.Model.Totp = totp;
                if (isNew)
                    draft.CreationTime = draft.LastModifiedTime;

                var savedCredential = draft;
                if (!await SaveCredentialAsync(async () =>
                    {
                        if (isNew)
                            await _credentialsCryptoService.AddCredential(draft.Model, groupId);
                        else
                            savedCredential = _credentialViewModelFactory.ProvideNew(
                                await _credentialsCryptoService.EditCredential(draft.Model, groupId, changeGroup));
                    }))
                {
                    if (login != null)
                        login.Model.Totp = originalTotp;
                    return;
                }

                var index = _credentialVMs.FindIndex(item => item.Model.Id == draft.Model.Id);
                if (index >= 0)
                    _credentialVMs[index] = savedCredential;
                else if (isNew)
                    _credentialVMs.Add(draft);

                var stillEditingDraft = ReferenceEquals(DraftCredential, draft);
                login?.ResetTotpEditing();
                if (stillEditingDraft)
                    DraftCredential = null;

                await DisplayCredentialsAsync(keepSelection: true,
                    preferredSelection: isNew && stillEditingDraft ? draft : null);
            }
            finally
            {
                IsSavingEdit = false;
            }
        }

        /// <summary>The TOTP setup the login editor asks for. False when the entered setup cannot be read.</summary>
        private static bool TryGetEditedTotp(LoginViewModel login, out TotpConfiguration totp)
        {
            totp = login.Model.Totp;
            if (login.RemoveTotp)
            {
                totp = null;
                return true;
            }

            if (login.ShowTotpSetup && (login.ReplaceTotp || !string.IsNullOrWhiteSpace(login.TotpSetupInput)))
                return TotpService.TryParse(login.TotpSetupInput ?? string.Empty, out totp);

            return true;
        }

        [RelayCommand(CanExecute = nameof(CanEditCredential))]
        private async Task Delete()
        {
            if (SelectedCredentialVM is not { } credential)
                return;

            var result = await MessageDialog.ShowAsync(
                Erapkey.Language.Properties.Resources.DeleteItem,
                string.Format(Erapkey.Language.Properties.Resources.Name0, credential.NameFieldVM.Value) +
                    Environment.NewLine + Environment.NewLine + MoveToTrashHint,
                MessageDialogButtons.YesNo,
                "\uE74D");

            if (result == DialogChoice.Yes)
                await DeleteCredentialAsync(credential);
        }

        /// <summary>What happens to a deleted entry, shown when deletion is confirmed.</summary>
        public static string MoveToTrashHint =>
            string.Format(LocalizationManager.Instance["MoveToTrashHint"], TrashViewModel.RetentionDays);

        /// <summary>Moves the entry to the trash ("Deleted"), where it can be restored for a while.</summary>
        internal async Task<bool> DeleteCredentialAsync(CredentialViewModel credential)
        {
            if (!await SaveCredentialAsync(() => _credentialsCryptoService.MoveToTrash(credential.Model)))
                return false;

            StatusMessageRequested?.Invoke(string.Format(LocalizationManager.Instance["MovedToTrash"], credential.NameFieldVM.Value));

            _credentialVMs.Remove(credential);
            var index = Math.Min(DisplayedCredentials.IndexOf(credential), DisplayedCredentials.Count - 2);
            await DisplayCredentialsAsync();
            if (index >= 0 && index < DisplayedCredentials.Count)
                SelectedCredentialVM = DisplayedCredentials[index];
            return true;
        }

        private async Task<bool> SaveCredentialAsync(Func<Task> save)
        {
            try
            {
                await save();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save the password vault.");
                StatusMessageRequested?.Invoke(LocalizationManager.Instance["SavePasswordVaultFailed"]);
                return false;
            }
        }

        [RelayCommand]
        private void CopyToClipboard(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;

            try
            {
                WindowsClipboard.SetText(data);
                StatusMessageRequested?.Invoke(Erapkey.Language.Properties.Resources.TextCopied);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        [RelayCommand]
        private void CopyTotp()
        {
            var configuration = IsEditing
                ? DraftCredential?.Login is { ShowCurrentTotp: true } draft ? draft.Totp : null
                : SelectedCredentialVM?.Login?.Totp;
            if (TotpService.TryGetCode(configuration, DateTime.UtcNow, out var code, out _))
                CopyToClipboard(code);
        }

        #region Hosts
        /// <summary>An SSH key entry a host can sign in with; the first option is "no key".</summary>
        public sealed record SshKeyOption(Guid? Id, string Name, string KeyType, bool IsMissing = false);

        /// <summary>The SSH key entries, for the key picker of the host editor and the host view.</summary>
        public IReadOnlyList<SshKeyOption> SshKeyOptions { get; private set; } = [];

        private void RefreshSshKeyOptions()
        {
            var options = new List<SshKeyOption> { new(null, LocalizationManager.Instance["NoSshKey"], null) };
            options.AddRange(_credentialVMs
                .Where(item => item.SshKey != null)
                .OrderBy(item => item.NameFieldVM.Value, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => new SshKeyOption(item.Model.Id, item.NameFieldVM.Value, item.SshKey.KeyInfo?.KeyType)));

            // A new list only when something changed: the picker of an open editor would lose its selection.
            if (options.SequenceEqual(SshKeyOptions))
                return;

            SshKeyOptions = options;
            OnPropertyChanged(nameof(SshKeyOptions));
        }

        [RelayCommand]
        private void CopySshCommand(HostViewModel host)
        {
            if (host is { HasHostname: true })
                CopyToClipboard(SshCommands.BuildCommandLine(GetSshArguments(host)));
        }

        /// <summary>Opens the connection in Windows Terminal (a tab of the Erapkey window) or a console window.</summary>
        [RelayCommand]
        private void ConnectSsh(HostViewModel host)
        {
            if (host is not { HasHostname: true })
                return;

            var title = _credentialVMs.Find(item => ReferenceEquals(item.Host, host))?.NameFieldVM.Value;
            try
            {
                SshCommands.Connect(string.IsNullOrWhiteSpace(title) ? host.Destination : title, GetSshArguments(host));
            }
            catch (System.IO.FileNotFoundException)
            {
                StatusMessageRequested?.Invoke(LocalizationManager.Instance["SshClientMissing"]);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                _logger.LogError(ex, "Failed to start the SSH connection");
                StatusMessageRequested?.Invoke(string.Format(LocalizationManager.Instance["ConnectSshFailed"], ex.Message));
            }
        }

        /// <summary>ssh arguments for the host; with a linked key and a running agent, sign-in goes through the agent.</summary>
        private IReadOnlyList<string> GetSshArguments(HostViewModel host)
        {
            string identityFile = null;
            if (_sshAgent?.IsRunning == true && host.SshKeyId is { } keyId &&
                _credentialVMs.Find(item => item.Model.Id == keyId)?.SshKey is { HasPublicKey: true } key)
            {
                try
                {
                    identityFile = SshCommands.WritePublicKey(keyId, key.PublicKey);
                }
                catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "Failed to write the public key file");
                }
            }

            return SshCommands.BuildArguments(host, identityFile, identityFile == null ? null : _sshAgent.IdentityAgentPath);
        }
        #endregion

        private static bool CanOpenInBrowser(Uri address) =>
            address?.IsAbsoluteUri == true &&
            (address.Scheme == Uri.UriSchemeHttp || address.Scheme == Uri.UriSchemeHttps);

        [RelayCommand(CanExecute = nameof(CanOpenInBrowser))]
        private void OpenInBrowser(Uri address)
        {
            if (!CanOpenInBrowser(address))
                return;

            Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
        }
    }
}
