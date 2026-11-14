using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.WPF.Models;
using SinglePass.WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace SinglePass.WPF.ViewModels
{
    [DebuggerDisplay("{Model}")]
    public partial class CredentialViewModel : ObservableRecipient
    {
        #region Design time instance
        private static readonly Lazy<CredentialViewModel> _lazy = new(GetDesignTimeVM);
        public static CredentialViewModel DesignTimeInstance => _lazy.Value;
        private static CredentialViewModel GetDesignTimeVM()
        {
            var additionalFields = new List<PassField>() { new PassField() { Name = "Design additional field", Value = "Test value" } };
            var model = Credential.CreateNew();
            model.AdditionalFields = additionalFields;

            var vm = new CredentialViewModel(model, null);
            return vm;
        }
        #endregion

        private readonly IFavIconCollector _favIconCollector;

        public Credential Model { get; }
        public TotpConfiguration Totp => Model.Totp;

        [ObservableProperty]
        public partial string TotpSetupInput { get; set; }

        [ObservableProperty]
        public partial bool RemoveTotp { get; set; }

        [ObservableProperty]
        public partial bool HasInvalidTotp { get; set; }

        partial void OnTotpSetupInputChanged(string value) => HasInvalidTotp = false;
        partial void OnRemoveTotpChanged(bool value) => HasInvalidTotp = false;
        public PassFieldViewModel NameFieldVM { get; }
        public PassFieldViewModel LoginFieldVM { get; }
        public PassFieldViewModel PasswordFieldVM { get; }
        public PassFieldViewModel OtherFieldVM { get; }
        public PassFieldViewModel SiteFieldVM { get; }
        public ObservableCollection<PassFieldViewModel> AdditionalFields { get; }

        public string WebsiteDomain
        {
            get
            {
                var site = SiteFieldVM.Value?.Trim();
                if (string.IsNullOrEmpty(site))
                    return null;

                if (site.StartsWith("//", StringComparison.Ordinal))
                    site = "https:" + site;
                else if (!site.Contains("://", StringComparison.Ordinal))
                    site = "https://" + site;

                return Uri.TryCreate(site, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                    ? uri.Host
                    : null;
            }
        }

        public DateTime LastModifiedTime
        {
            get => Model.LastModifiedTime;
            set => Model.LastModifiedTime = value;
        }

        public DateTime CreationTime
        {
            get => Model.CreationTime;
            set => Model.CreationTime = value;
        }

        [ObservableProperty]
        public partial bool PasswordVisible { get; set; }

        private ImageSource _favIcon;
        public ImageSource FavIcon
        {
            get
            {
                if (_favIcon is null && WebsiteDomain is { } domain)
                    _favIconCollector?.ScheduleGetImage(
                        "https://" + domain,
                        (image) => SetProperty(ref _favIcon, image, nameof(FavIcon)));

                return _favIcon;
            }
        }

        public CredentialViewModel(Credential credential, IFavIconCollector favIconCollector)
        {
            Model = credential ?? throw new ArgumentNullException(nameof(credential));
            _favIconCollector = favIconCollector;

            NameFieldVM = new PassFieldViewModel(credential.NameField);
            LoginFieldVM = new PassFieldViewModel(credential.LoginField);
            PasswordFieldVM = new PassFieldViewModel(credential.PasswordField);
            OtherFieldVM = new PassFieldViewModel(credential.OtherField);
            SiteFieldVM = new PassFieldViewModel(credential.SiteField);
            SiteFieldVM.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(PassFieldViewModel.Value))
                    OnPropertyChanged(nameof(WebsiteDomain));
            };
            AdditionalFields = new ObservableCollection<PassFieldViewModel>(credential.AdditionalFields.Select(f => new PassFieldViewModel(f)));
            LastModifiedTime = credential.LastModifiedTime;
            CreationTime = credential.CreationTime;
        }

        internal CredentialViewModel Clone()
        {
            var cloneModel = Model.Clone();
            var cloneVM = new CredentialViewModel(cloneModel, _favIconCollector)
            {
                PasswordVisible = PasswordVisible,
            };
            return cloneVM;
        }
    }
}
