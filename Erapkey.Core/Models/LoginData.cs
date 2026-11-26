using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Erapkey.Core.Models
{
    /// <summary>Data of a <see cref="CredentialType.Login"/> entry.</summary>
    public sealed class LoginData
    {
        internal const int PasswordHistoryLimit = 10;

        public PassField LoginField { get; set; } = new PassField() { Name = "Login" };
        public PassField PasswordField { get; set; } = new PassField() { Name = "Password" };
        public PassField SiteField { get; set; } = new PassField() { Name = "Site" };
        public TotpConfiguration Totp { get; set; }

        /// <summary>Up to <see cref="PasswordHistoryLimit"/> previous passwords, newest first. Kept by the vault, not the UI.</summary>
        [JsonInclude]
        public IReadOnlyList<PasswordHistoryEntry> PasswordHistory { get; internal set; } = [];

        public LoginData Clone() => new()
        {
            LoginField = LoginField.Clone(),
            PasswordField = PasswordField.Clone(),
            SiteField = SiteField.Clone(),
            Totp = Totp,
            PasswordHistory = PasswordHistory,
        };
    }
}
