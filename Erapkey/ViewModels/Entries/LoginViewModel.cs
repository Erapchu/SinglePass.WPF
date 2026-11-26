using CommunityToolkit.Mvvm.ComponentModel;
using Erapkey.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Erapkey.ViewModels.Entries
{
    public sealed partial class LoginViewModel : CredentialDataViewModel
    {
        public LoginData Model { get; }

        public PassFieldViewModel LoginFieldVM { get; }
        public PassFieldViewModel PasswordFieldVM { get; }
        public PassFieldViewModel SiteFieldVM { get; }

        public TotpConfiguration Totp => Model.Totp;
        public IReadOnlyList<PasswordHistoryEntry> PasswordHistory => Model.PasswordHistory;

        public LoginViewModel(LoginData model)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            LoginFieldVM = new PassFieldViewModel(model.LoginField);
            PasswordFieldVM = new PassFieldViewModel(model.PasswordField);
            SiteFieldVM = new PassFieldViewModel(model.SiteField);
            LoginFieldVM.PropertyChanged += LoginField_PropertyChanged;
            SiteFieldVM.PropertyChanged += SiteField_PropertyChanged;
        }

        public override string Subtitle => LoginFieldVM.Value;
        public override IEnumerable<string> SearchableTexts => [LoginFieldVM.Value, SiteFieldVM.Value];

        private void LoginField_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PassFieldViewModel.Value))
                return;

            OnPropertyChanged(nameof(Subtitle));
            OnPropertyChanged(nameof(SubtitleToolTip));
        }

        private void SiteField_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PassFieldViewModel.Value))
                return;

            OnPropertyChanged(nameof(WebsiteDomain));
            OnPropertyChanged(nameof(Website));
            OnPropertyChanged(nameof(HasWebsite));
        }

        #region TOTP editing
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

        internal void ResetTotpEditing()
        {
            TotpSetupInput = null;
            RemoveTotp = false;
            ReplaceTotp = false;
        }
        #endregion

        #region Website
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

        // Shared with the browser extension's matching, so the details pane and the extension read the field alike.
        private static Uri ParseWebsite(string site, out int authorityStart) =>
            Core.Services.WebsiteMatcher.ParseEntrySite(site, out authorityStart);
        #endregion
    }
}
