using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.Core.Localization;
using SinglePass.Core.Models;
using SinglePass.Core.Services;
using SinglePass.WPF.Services;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace SinglePass.WPF.ViewModels
{
    /// <summary>
    /// The "Deleted" page: entries in the trash, which can be restored or deleted for good.
    /// Entries left in the trash are deleted for good after <see cref="PasswordVault.TrashRetention"/>.
    /// </summary>
    public partial class TrashViewModel : ObservableObject
    {
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly CredentialViewModelFactory _credentialViewModelFactory;
        private readonly ILogger<TrashViewModel> _logger;

        public ObservableCollection<TrashItemViewModel> Items { get; } = [];

        public bool HasItems => Items.Count > 0;

        public string CountText => Plural(Items.Count, "TrashItemsCount");

        public static int RetentionDays => (int)PasswordVault.TrashRetention.TotalDays;

        public string Description => string.Format(LocalizationManager.Instance["TrashDescription"], RetentionDays);

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RestoreCommand))]
        [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
        [NotifyCanExecuteChangedFor(nameof(RestoreAllCommand))]
        [NotifyCanExecuteChangedFor(nameof(EmptyTrashCommand))]
        public partial bool IsBusy { get; private set; }

        /// <summary>Entries came back to the vault or the trash changed: the list of entries has to be reloaded.</summary>
        public event Action VaultChanged;

        public event Action<string> StatusMessageRequested;

        public TrashViewModel(
            CredentialsCryptoService credentialsCryptoService,
            CredentialViewModelFactory credentialViewModelFactory,
            ILogger<TrashViewModel> logger)
        {
            _credentialsCryptoService = credentialsCryptoService;
            _credentialViewModelFactory = credentialViewModelFactory;
            _logger = logger;
            Items.CollectionChanged += (_, _) =>
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(CountText));
                RestoreAllCommand.NotifyCanExecuteChanged();
                EmptyTrashCommand.NotifyCanExecuteChanged();
            };
            PropertyChangedEventManager.AddHandler(LocalizationManager.Instance, OnLanguageChanged, "Item[]");
        }

        /// <summary>Deletes expired entries and shows what is left in the trash.</summary>
        public async Task RefreshAsync()
        {
            try
            {
                if (!IsBusy)
                    await _credentialsCryptoService.PurgeExpiredTrash();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to purge expired entries from the trash.");
            }

            ShowItems();
        }

        private void ShowItems()
        {
            var now = DateTimeOffset.UtcNow;
            Items.Clear();
            foreach (var item in _credentialsCryptoService.TrashedCredentials)
                Items.Add(new TrashItemViewModel(_credentialViewModelFactory.ProvideNew(item.Credential), item.PurgeAt, now));
        }

        private bool CanChange(TrashItemViewModel item) => !IsBusy && item != null;

        [RelayCommand(CanExecute = nameof(CanChange))]
        private Task Restore(TrashItemViewModel item) =>
            ChangeAsync(() => _credentialsCryptoService.RestoreFromTrash([item.Id]),
                string.Format(LocalizationManager.Instance["TrashItemRestored"], item.Name));

        [RelayCommand(CanExecute = nameof(CanChange))]
        private async Task Delete(TrashItemViewModel item)
        {
            var loc = LocalizationManager.Instance;
            var choice = await MessageDialog.ShowAsync(loc["DeleteForeverTitle"],
                string.Format(loc["DeleteForeverConfirmation"], item.Name), MessageDialogButtons.YesNo, "");
            if (choice == DialogChoice.Yes)
                await ChangeAsync(() => _credentialsCryptoService.DeleteCredentials([item.Id]), null);
        }

        private bool CanChangeAll() => !IsBusy && Items.Count > 0;

        [RelayCommand(CanExecute = nameof(CanChangeAll))]
        private Task RestoreAll() =>
            ChangeAsync(() => _credentialsCryptoService.RestoreFromTrash(Ids()),
                Plural(Items.Count, "TrashItemsRestored"));

        [RelayCommand(CanExecute = nameof(CanChangeAll))]
        private async Task EmptyTrash()
        {
            var loc = LocalizationManager.Instance;
            var choice = await MessageDialog.ShowAsync(loc["EmptyTrashTitle"],
                string.Format(loc["EmptyTrashConfirmation"], CountText), MessageDialogButtons.YesNo, "");
            if (choice == DialogChoice.Yes)
                await ChangeAsync(() => _credentialsCryptoService.DeleteCredentials(Ids()), null);
        }

        private List<Guid> Ids() => Items.Select(item => item.Id).ToList();

        private async Task ChangeAsync(Func<Task> change, string status)
        {
            if (IsBusy)
                return;

            IsBusy = true;
            try
            {
                await change();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to change the trash.");
                StatusMessageRequested?.Invoke(LocalizationManager.Instance["SavePasswordVaultFailed"]);
                return;
            }
            finally
            {
                IsBusy = false;
            }

            ShowItems();
            VaultChanged?.Invoke();
            if (status != null)
                StatusMessageRequested?.Invoke(status);
        }

        private void OnLanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(Description));
            foreach (var item in Items)
                item.RefreshText();
        }

        /// <summary>
        /// Picks the plural form of a counted phrase: <paramref name="key"/> + One, Few or Many.
        /// Russian uses all three; English uses One and Many.
        /// </summary>
        internal static string Plural(int count, string key)
        {
            var form = "Many";
            var culture = LocalizationManager.Instance.Culture;
            if (culture.TwoLetterISOLanguageName == "ru")
            {
                var lastTwo = count % 100;
                var last = count % 10;
                if (last == 1 && lastTwo != 11)
                    form = "One";
                else if (last is >= 2 and <= 4 && lastTwo is < 12 or > 14)
                    form = "Few";
            }
            else if (count == 1)
            {
                form = "One";
            }

            return string.Format(culture, LocalizationManager.Instance[key + form], count);
        }
    }

    /// <summary>An entry on the "Deleted" page.</summary>
    public sealed class TrashItemViewModel : ObservableObject
    {
        public CredentialViewModel Credential { get; }

        internal Guid Id => Credential.Model.Id;

        public string Name => Credential.NameFieldVM.Value;

        /// <summary>Whole days until the entry is deleted for good, at least one.</summary>
        public int DaysLeft { get; }

        /// <summary>Second line: entry type and days left, for example "Password · 26 days left".</summary>
        public string Details
        {
            get
            {
                var loc = LocalizationManager.Instance;
                var type = Credential.Type switch
                {
                    CredentialType.SshKey => loc["SshKey"],
                    CredentialType.Card => loc["Card"],
                    CredentialType.Host => loc["Host"],
                    _ => loc["Password"]
                };
                return $"{type} · {TrashViewModel.Plural(DaysLeft, "TrashDaysLeft")}";
            }
        }

        internal TrashItemViewModel(CredentialViewModel credential, DateTimeOffset purgeAt, DateTimeOffset now)
        {
            Credential = credential;
            DaysLeft = Math.Max(1, (int)Math.Ceiling((purgeAt - now).TotalDays));
        }

        internal void RefreshText() => OnPropertyChanged(nameof(Details));
    }
}
