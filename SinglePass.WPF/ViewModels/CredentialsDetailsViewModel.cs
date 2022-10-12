using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SinglePass.WPF.Enums;
using SinglePass.WPF.Helpers;
using SinglePass.WPF.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SinglePass.WPF.ViewModels
{
    [INotifyPropertyChanged]
    public partial class CredentialsDetailsViewModel
    {
        #region Design time instance
        private static readonly Lazy<CredentialsDetailsViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialsDetailsViewModel DesignTimeInstance => _lazy.Value;
        private static CredentialsDetailsViewModel GetDesignTimeVM()
        {
            var additionalFields = new List<PassField>() { new PassField() { Name = "Design additional field", Value = "Test value" } };
            var model = Credential.CreateNew();
            model.AdditionalFields = additionalFields;

            var credVm = new CredentialViewModel(model, null);
            var vm = new CredentialsDetailsViewModel(null);
            vm._credentialViewModel = credVm;
            vm.Mode = CredentialDetailsMode.View;
            return vm;
        }
        #endregion

        private readonly ILogger<CredentialsDetailsViewModel> _logger;

        public event Action<CredentialViewModel, CredentialDetailsMode> Accepted;
        public event Action<CredentialViewModel> Deleted;
        public event Action Cancelled;
        public event Action<string> EnqueueSnackbarMessage;

        [ObservableProperty]
        private CredentialViewModel _credentialViewModel;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CaptionText))]
        private CredentialDetailsMode _mode = CredentialDetailsMode.View;

        [ObservableProperty]
        private bool _isPasswordVisible;

        [ObservableProperty]
        private bool _isNameTextBoxFocused;

        public string CaptionText
        {
            get
            {
                switch (_mode)
                {
                    case CredentialDetailsMode.Edit:
                        return SinglePass.Language.Properties.Resources.Edit;
                    case CredentialDetailsMode.New:
                        return SinglePass.Language.Properties.Resources.New;
                    case CredentialDetailsMode.View:
                        return SinglePass.Language.Properties.Resources.Details;
                    default:
                        break;
                }

                return string.Empty;
            }
        }

        public CredentialsDetailsViewModel(ILogger<CredentialsDetailsViewModel> logger)
        {
            _logger = logger;
        }

        [RelayCommand]
        private void Ok()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            Accepted?.Invoke(CredentialViewModel, Mode);
        }

        [RelayCommand]
        private void Cancel()
        {
            Cancelled?.Invoke();
        }

        [RelayCommand]
        private void Edit()
        {
            if (CredentialViewModel is null)
                return;

            CredentialViewModel = CredentialViewModel.Clone();
            Mode = CredentialDetailsMode.Edit;
            IsPasswordVisible = true;
            SetFocus();
        }

        [RelayCommand]
        private void Delete()
        {
            if (CredentialViewModel is null)
                return;

            Deleted?.Invoke(CredentialViewModel);
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
            var uri = CredentialViewModel?.SiteFieldVM?.Value;
            if (string.IsNullOrWhiteSpace(uri))
                return;

            uri = uri.Replace("&", "^&");
            if (Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out var site))
            {
                Process.Start(new ProcessStartInfo("cmd", $"/c start {site}") { CreateNoWindow = true });
            }
        }

        public void SetFocus()
        {
            IsNameTextBoxFocused = false;
            IsNameTextBoxFocused = true;
        }
    }
}
