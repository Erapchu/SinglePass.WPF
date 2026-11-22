using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.Language.Properties;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Hotkeys;
using SinglePass.Core.Localization;
using SinglePass.WPF.Services;
using SinglePass.WPF.Settings;
using SinglePass.WPF.ViewModels.Dialogs;
using SinglePass.WPF.Views.Controls;
using SinglePass.WPF.Views.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SinglePass.Core.Helpers;
using SinglePass.Core.Services;

namespace SinglePass.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        #region Design time instance
        private static readonly Lazy<SettingsViewModel> _lazy = new(GetDesignTimeVM);
        public static SettingsViewModel DesignTimeInstance => _lazy.Value;

        private static SettingsViewModel GetDesignTimeVM()
        {
            var vm = new SettingsViewModel();
            return vm;
        }
        #endregion

        private readonly ThemeService _themeService;
        private readonly AppSettingsService _appSettingsService;
        private readonly CredentialsCryptoService _credentialsCryptoService;
        private readonly HotkeysService _hotkeysService;
        private readonly SshAgentServer _sshAgent;
        private readonly ILogger<SettingsViewModel> _logger;

        /// <summary>Raised after a backup restore changed the vault, so the lists can reload it.</summary>
        public event Action VaultRestored;

        /// <summary>Vault backups, newest first.</summary>
        public ObservableCollection<BackupItem> Backups { get; } = new();

        [ObservableProperty]
        public partial bool RestoringBackup { get; set; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ChangeHelperPopupHotkeyCommand))]
        public partial Hotkey ShowPopupHotkey { get; set; }

        /// <summary>The popup hotkey is registered. Off by default.</summary>
        [ObservableProperty]
        public partial bool PopupHotkeyEnabled { get; set; }

        [ObservableProperty]
        public partial AppTheme ThemeMode { get; set; }

        [ObservableProperty]
        public partial bool AnimationsEnabled { get; set; } = true;

        [ObservableProperty]
        public partial string Language { get; set; } = "system";

        [ObservableProperty]
        public partial IdleLockTimeout IdleLockTimeout { get; set; } = IdleLockTimeout.FiveMinutes;

        [ObservableProperty]
        public partial bool LockOnWindowsLock { get; set; } = true;

        [ObservableProperty]
        public partial bool SshAgentEnabled { get; set; } = true;

        /// <summary>The agent answers on its pipe. False when it is turned off or could not start.</summary>
        public bool IsSshAgentRunning => _sshAgent?.IsRunning == true;

        /// <summary>Value for <c>SSH_AUTH_SOCK</c> or <c>IdentityAgent</c>.</summary>
        public string SshAgentPipePath => _sshAgent?.PipePath;

        private SettingsViewModel() { }

        public SettingsViewModel(
            ThemeService themeService,
            AppSettingsService appSettingsService,
            CredentialsCryptoService credentialsCryptoService,
            HotkeysService hotkeysService,
            SshAgentServer sshAgent,
            ILogger<SettingsViewModel> logger)
        {
            // Before _sshAgent is set: loading the setting must not start or stop the agent.
            SshAgentEnabled = appSettingsService.Settings.SshAgentEnabled;
            _sshAgent = sshAgent;
            ThemeMode = appSettingsService.ThemeMode;
            AnimationsEnabled = appSettingsService.Settings.AnimationsEnabled;
            Language = LocalizationManager.Normalize(appSettingsService.Language);
            ShowPopupHotkey = appSettingsService.ShowPopupHotkey;
            // Before _hotkeysService is set: loading the setting must not register or save anything.
            PopupHotkeyEnabled = appSettingsService.PopupHotkeyEnabled;
            IdleLockTimeout = appSettingsService.Settings.IdleLockTimeout;
            LockOnWindowsLock = appSettingsService.Settings.LockOnWindowsLock;

            _themeService = themeService;
            _appSettingsService = appSettingsService;
            _credentialsCryptoService = credentialsCryptoService;
            _hotkeysService = hotkeysService;
            _logger = logger;
        }

        [RelayCommand]
        private void LoadBackups()
        {
            if (_credentialsCryptoService is null)
                return;

            Backups.Clear();
            try
            {
                foreach (var backup in _credentialsCryptoService.Backups)
                    Backups.Add(new BackupItem(backup));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogError(ex, "Failed to list vault backups");
            }
        }

        [RelayCommand]
        private void OpenBackupsFolder()
        {
            try
            {
                Directory.CreateDirectory(Constants.BackupsDirectoryPath);
                Process.Start(new ProcessStartInfo(Constants.BackupsDirectoryPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open the backups folder");
            }
        }

        [RelayCommand]
        private async Task RestoreBackup(BackupItem item)
        {
            if (item is null || RestoringBackup)
                return;

            var confirm = await MessageDialog.ShowAsync(
                LocalizationManager.Instance["RestoreBackupTitle"],
                string.Format(LocalizationManager.Instance["RestoreBackupConfirm"], item.CreatedAt),
                MessageDialogButtons.OKCancel,
                "\uE777");
            if (confirm != DialogChoice.OK)
                return;

            RestoringBackup = true;
            try
            {
                // Backups made before a password change open only with the password of that time.
                var backup = await _credentialsCryptoService.ReadBackup(item.Info.Path, _credentialsCryptoService.GetPassword());
                while (backup is null)
                {
                    var dialog = new SyncPasswordDialog();
                    await ModalDialog.ShowAsync(dialog);
                    if (dialog.Password is null)
                        return;

                    backup = await _credentialsCryptoService.ReadBackup(item.Info.Path, dialog.Password);
                }

                var result = await _credentialsCryptoService.RestoreBackup(backup);
                LoadBackups();
                if (result.AnyChanges)
                    VaultRestored?.Invoke();

                await MessageDialog.ShowAsync(
                    Resources.Success,
                    result.AnyChanges
                        ? string.Format(LocalizationManager.Instance["BackupRestored"], result.Restored.Count, result.Reverted.Count)
                        : LocalizationManager.Instance["BackupNothingToRestore"],
                    MessageDialogButtons.OK,
                    "\uE73E");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore a vault backup");
                await MessageDialog.ShowAsync(Resources.Error, LocalizationManager.Instance["BackupRestoreFailed"],
                    MessageDialogButtons.OK, "\uEA39");
            }
            finally
            {
                RestoringBackup = false;
            }
        }

        [RelayCommand]
        private async Task ChangePassword()
        {
            var dialog = new ChangePasswordDialog(_credentialsCryptoService);
            await ModalDialog.ShowAsync(dialog);
            if (dialog.Changed)
            {
                await MessageDialog.ShowAsync(
                    Resources.Success,
                    LocalizationManager.Instance["NewPasswordApplied"],
                    MessageDialogButtons.OK,
                    "\uE73E");
            }
        }

        [RelayCommand]
        private void ChangeHelperPopupHotkey(System.Windows.Input.KeyEventArgs args)
        {
            if (!_hotkeysService.GetHotkeyForKeyPress(args, out Hotkey hotkey))
                return;

            if (_hotkeysService.ApplyPopupHotkey(PopupHotkeyEnabled, hotkey))
            {
                ShowPopupHotkey = hotkey;
                return;
            }

            // Keep the previous hotkey: the new one belongs to another program.
            _hotkeysService.ApplyPopupHotkey(PopupHotkeyEnabled, ShowPopupHotkey);
            ShowHotkeyInUse(hotkey);
        }

        [RelayCommand]
        private void ClearShowPopupHotkey()
        {
            _hotkeysService?.ApplyPopupHotkey(PopupHotkeyEnabled, Hotkey.Empty);
            ShowPopupHotkey = Hotkey.Empty;
        }

        partial void OnPopupHotkeyEnabledChanged(bool value)
        {
            if (_hotkeysService is null)
                return;

            if (!_hotkeysService.ApplyPopupHotkey(value, ShowPopupHotkey))
                ShowHotkeyInUse(ShowPopupHotkey);

            SavePopupHotkeySettings();
        }

        private static void ShowHotkeyInUse(Hotkey hotkey)
        {
            _ = MessageDialog.ShowAsync(Resources.Error,
                string.Format(LocalizationManager.Instance["HotkeyInUse"], hotkey),
                MessageDialogButtons.OK, "\uEA39");
        }

        private async void SavePopupHotkeySettings()
        {
            _appSettingsService.PopupHotkeyEnabled = PopupHotkeyEnabled;
            _appSettingsService.ShowPopupHotkey = ShowPopupHotkey;
            try
            {
                await _appSettingsService.Save();
            }
            catch (Exception)
            {
                await MessageDialog.ShowAsync(Resources.Error, LocalizationManager.Instance["SaveHotkeySettingsFailed"],
                    MessageDialogButtons.OK, "\uEA39");
            }
        }

        partial void OnLanguageChanged(string value) => LocalizationManager.Instance.Apply(value);

        partial void OnThemeModeChanged(AppTheme value) => _themeService?.Apply(value);

        partial void OnAnimationsEnabledChanged(bool value) => SaveAnimationsSetting(value);

        private async void SaveAnimationsSetting(bool enabled)
        {
            if (_appSettingsService is null)
                return;

            _appSettingsService.Settings.AnimationsEnabled = enabled;
            try
            {
                await _appSettingsService.Save();
            }
            catch (Exception)
            {
                await MessageDialog.ShowAsync(Resources.Error, LocalizationManager.Instance["SaveAppearanceSettingsFailed"],
                    MessageDialogButtons.OK, "\uEA39");
            }
        }

        partial void OnIdleLockTimeoutChanged(IdleLockTimeout value) => SaveSecuritySettings();

        partial void OnLockOnWindowsLockChanged(bool value) => SaveSecuritySettings();

        partial void OnSshAgentEnabledChanged(bool value)
        {
            if (_sshAgent is null)
                return;

            if (value)
            {
                try
                {
                    _sshAgent.Start();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogError(ex, "The SSH agent could not start");
                    _ = MessageDialog.ShowAsync(Resources.Error, ex.Message, MessageDialogButtons.OK, "");
                }
            }
            else
                _sshAgent.Stop();

            OnPropertyChanged(nameof(IsSshAgentRunning));
            SaveSecuritySettings();
        }

        [RelayCommand]
        private void CopySshAgentPipePath()
        {
            try
            {
                WindowsClipboard.SetText(SshAgentPipePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, string.Empty);
            }
        }

        private async void SaveSecuritySettings()
        {
            if (_appSettingsService is null)
                return;

            _appSettingsService.Settings.IdleLockTimeout = IdleLockTimeout;
            _appSettingsService.Settings.LockOnWindowsLock = LockOnWindowsLock;
            _appSettingsService.Settings.SshAgentEnabled = SshAgentEnabled;
            try
            {
                await _appSettingsService.Save();
            }
            catch (Exception)
            {
                await MessageDialog.ShowAsync(Resources.Error, LocalizationManager.Instance["SaveSecuritySettingsFailed"],
                    MessageDialogButtons.OK, "\uEA39");
            }
        }
    }

    /// <summary>A vault backup in the Security settings list.</summary>
    public sealed class BackupItem(VaultBackupInfo info)
    {
        internal VaultBackupInfo Info { get; } = info;
        public DateTime CreatedAt { get; } = info.CreatedAt.LocalDateTime;
        public string Reason { get; } = LocalizationManager.Instance["BackupReason" + info.Reason];
    }
}
