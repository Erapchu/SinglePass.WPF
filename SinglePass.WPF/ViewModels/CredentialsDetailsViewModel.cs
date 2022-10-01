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
    public class CredentialsDetailsViewModel : ObservableRecipient
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

        public event Action<CredentialViewModel, CredentialDetailsMode> Accept;
        public event Action<CredentialViewModel> Delete;
        public event Action Cancel;
        public event Action<string> EnqueueSnackbarMessage;

        private RelayCommand _okCommand;
        private RelayCommand _cancelCommand;
        private RelayCommand _editCommand;
        private RelayCommand _deleteCommand;
        private RelayCommand _openInBrowserCommand;
        private RelayCommand<string> _copyToClipboardCommand;
        private CredentialViewModel _credentialViewModel;
        private CredentialDetailsMode _mode = CredentialDetailsMode.View;
        private bool _isPasswordVisible;
        private bool _isNameTextBoxFocused;

        public RelayCommand OkCommand => _okCommand ??= new RelayCommand(OkExecute);
        public RelayCommand CancelCommand => _cancelCommand ??= new RelayCommand(CancelExecute);
        public RelayCommand EditCommand => _editCommand ??= new RelayCommand(EditExecute);
        public RelayCommand DeleteCommand => _deleteCommand ??= new RelayCommand(DeleteExecute);
        public RelayCommand<string> CopyToClipboardCommand => _copyToClipboardCommand ??= new RelayCommand<string>(CopyToClipboard);
        public RelayCommand OpenInBrowserCommand => _openInBrowserCommand ??= new RelayCommand(OpenInBrowser);

        public CredentialViewModel CredentialViewModel
        {
            get => _credentialViewModel;
            set => SetProperty(ref _credentialViewModel, value);
        }

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

        public CredentialDetailsMode Mode
        {
            get => _mode;
            set
            {
                SetProperty(ref _mode, value);
                OnPropertyChanged(nameof(CaptionText));
            }
        }

        public bool IsPasswordVisible
        {
            get => _isPasswordVisible;
            set => SetProperty(ref _isPasswordVisible, value);
        }

        public bool IsNameTextBoxFocused
        {
            get => _isNameTextBoxFocused;
            set => SetProperty(ref _isNameTextBoxFocused, value);
        }

        public CredentialsDetailsViewModel(ILogger<CredentialsDetailsViewModel> logger)
        {
            _logger = logger;
        }

        private void OkExecute()
        {
            CredentialViewModel.NameFieldVM.ValidateValue();
            if (CredentialViewModel.NameFieldVM.HasErrors)
                return;

            Accept?.Invoke(CredentialViewModel, Mode);
        }

        private void CancelExecute()
        {
            Cancel?.Invoke();
        }

        private void EditExecute()
        {
            if (CredentialViewModel is null)
                return;

            CredentialViewModel = CredentialViewModel.Clone();
            Mode = CredentialDetailsMode.Edit;
            IsPasswordVisible = true;
            SetFocus();
        }

        private void DeleteExecute()
        {
            if (CredentialViewModel is null)
                return;

            Delete?.Invoke(CredentialViewModel);
        }

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
