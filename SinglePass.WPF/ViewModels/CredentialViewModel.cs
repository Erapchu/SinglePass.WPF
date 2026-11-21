using CommunityToolkit.Mvvm.ComponentModel;
using SinglePass.Core.Models;
using SinglePass.Core.Services;
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

        public CredentialType Type => Model.Type;
        public bool IsLogin => Model.Type == CredentialType.Login;
        public bool IsSshKey => Model.Type == CredentialType.SshKey;

        #region SSH key
        public string SshPrivateKey
        {
            get => Model.SshKey?.PrivateKey;
            set
            {
                if (Model.SshKey is not { } key || key.PrivateKey == value)
                    return;

                // Keep the public key in step with the private one, unless the user entered a different one.
                var followsPrivateKey = !HasSshPublicKey ||
                    (SshKeys.TryGetPublicKey(key.PrivateKey, out var previous) && SameKey(previous, key.PublicKey));
                key.PrivateKey = value;
                OnPropertyChanged();
                if (followsPrivateKey && SshKeys.TryGetPublicKey(value, out var derived) && !SameKey(derived, SshPublicKey))
                    SshPublicKey = derived;
                OnPropertyChanged(nameof(HasSshPrivateKey));
                OnPropertyChanged(nameof(IsSshKeyEncrypted));
            }
        }

        public string SshPublicKey
        {
            get => Model.SshKey?.PublicKey;
            set
            {
                if (Model.SshKey is not { } key || key.PublicKey == value)
                    return;

                key.PublicKey = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSshPublicKey));
                OnPropertyChanged(nameof(SshKeyInfo));
                OnPropertyChanged(nameof(HasSshKeyInfo));
            }
        }

        public string SshPassphrase
        {
            get => Model.SshKey?.Passphrase;
            set
            {
                if (Model.SshKey is not { } key || key.Passphrase == value)
                    return;

                key.Passphrase = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSshPassphrase));
            }
        }

        public bool HasSshPrivateKey => !string.IsNullOrWhiteSpace(SshPrivateKey);
        public bool HasSshPublicKey => !string.IsNullOrWhiteSpace(SshPublicKey);
        public bool HasSshPassphrase => !string.IsNullOrEmpty(SshPassphrase);
        public bool IsSshKeyEncrypted => SshKeys.IsEncrypted(SshPrivateKey);

        public sealed record SshKeyDescription(string KeyType, string Fingerprint);

        /// <summary>Key type and fingerprint of <see cref="SshPublicKey"/>, or null when it cannot be read.</summary>
        public SshKeyDescription SshKeyInfo =>
            SshKeys.TryParsePublicKey(SshPublicKey, out var keyType, out var fingerprint, out _)
                ? new SshKeyDescription(keyType, fingerprint)
                : null;

        public bool HasSshKeyInfo => SshKeyInfo != null;

        /// <summary>Same key data; the comment after it does not matter.</summary>
        private static bool SameKey(string first, string second)
        {
            static string KeyPart(string line)
            {
                var parts = line?.Trim().Split((char[])null, 3, StringSplitOptions.RemoveEmptyEntries);
                return parts is { Length: >= 2 } ? parts[0] + " " + parts[1] : line?.Trim();
            }

            return KeyPart(first) == KeyPart(second);
        }
        #endregion

        [ObservableProperty]
        public partial string TotpSetupInput { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrentTotp))]
        [NotifyPropertyChangedFor(nameof(ShowTotpSetup))]
        public partial bool RemoveTotp { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowCurrentTotp))]
        [NotifyPropertyChangedFor(nameof(ShowTotpSetup))]
        public partial bool ReplaceTotp { get; set; }

        public bool ShowCurrentTotp => Totp != null && !RemoveTotp && !ReplaceTotp;
        public bool ShowTotpSetup => !RemoveTotp && (Totp == null || ReplaceTotp);

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

                return ParseWebsite(site, out _)?.Host;
            }
        }

        public bool HasWebsite => !string.IsNullOrWhiteSpace(SiteFieldVM.Value);

        public sealed record WebsiteDisplay(string Prefix, string Host, string Suffix, Uri Address);

        public WebsiteDisplay Website
        {
            get
            {
                var site = SiteFieldVM.Value?.Trim() ?? string.Empty;
                var address = ParseWebsite(site, out var authorityStart);
                if (address == null || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
                    return new WebsiteDisplay(site, string.Empty, string.Empty, null);

                var authorityEnd = site.IndexOfAny(['/', '?', '#', '\\'], authorityStart);
                if (authorityEnd < 0)
                    authorityEnd = site.Length;

                var authority = site.AsSpan(authorityStart, authorityEnd - authorityStart);
                var hostStart = authority.LastIndexOf('@') + 1;
                var hostAndPort = authority[hostStart..];
                var hostLength = hostAndPort.StartsWith("[", StringComparison.Ordinal)
                    ? hostAndPort.IndexOf(']') + 1
                    : hostAndPort.IndexOf(':');
                if (hostLength < 0)
                    hostLength = hostAndPort.Length;

                hostStart += authorityStart;
                return new WebsiteDisplay(site[..hostStart], site.Substring(hostStart, hostLength),
                    site[(hostStart + hostLength)..], address);
            }
        }

        private static Uri ParseWebsite(string site, out int authorityStart)
        {
            authorityStart = 0;
            if (string.IsNullOrEmpty(site))
                return null;

            if (site.StartsWith("//", StringComparison.Ordinal))
            {
                authorityStart = 2;
                site = "https:" + site;
            }
            else
            {
                var schemeEnd = site.IndexOf("://", StringComparison.Ordinal);
                var firstSeparator = site.IndexOfAny(['/', '?', '#', '\\']);
                if (schemeEnd >= 0 && firstSeparator == schemeEnd + 1)
                    authorityStart = schemeEnd + 3;
                else
                    site = "https://" + site;
            }

            return Uri.TryCreate(site, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
                ? uri
                : null;
        }

        // The model keeps UTC; the UI shows and groups by local time.
        public DateTime LastModifiedTime
        {
            get => Model.LastModifiedTime.ToLocalTime();
            set => Model.LastModifiedTime = value;
        }

        public DateTime CreationTime
        {
            get => Model.CreationTime.ToLocalTime();
            set => Model.CreationTime = value;
        }

        [ObservableProperty]
        public partial bool PasswordVisible { get; set; }

        private ImageSource _favIcon;
        public ImageSource FavIcon
        {
            get
            {
                if (_favIconCollector != null && WebsiteDomain is { } domain)
                    _favIconCollector.ScheduleGetImage(
                        "https://" + domain,
                        image =>
                        {
                            if (WebsiteDomain == domain)
                                SetProperty(ref _favIcon, image, nameof(FavIcon));
                        });

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
                {
                    OnPropertyChanged(nameof(WebsiteDomain));
                    OnPropertyChanged(nameof(Website));
                    OnPropertyChanged(nameof(HasWebsite));
                    _favIcon = null;
                    OnPropertyChanged(nameof(FavIcon));
                }
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
