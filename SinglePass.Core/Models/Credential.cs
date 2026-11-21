using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace SinglePass.Core.Models
{
    [DebuggerDisplay("{NameField.Value}")]
    public class Credential
    {
        internal const int PasswordHistoryLimit = 10;

        public Guid Id { get; init; }
        public CredentialType Type { get; set; }
        public PassField NameField { get; set; } = new PassField() { Name = "Name" };
        public PassField LoginField { get; set; } = new PassField() { Name = "Login" };
        public PassField PasswordField { get; set; } = new PassField() { Name = "Password" };
        [JsonInclude]
        public IReadOnlyList<PasswordHistoryEntry> PasswordHistory { get; internal set; } = [];
        public PassField OtherField { get; set; } = new PassField() { Name = "Other" };
        public PassField SiteField { get; set; } = new PassField() { Name = "Site" };
        public List<PassField> AdditionalFields { get; set; } = new List<PassField>();
        public TotpConfiguration Totp { get; set; }
        /// <summary>Set for <see cref="CredentialType.SshKey"/> entries only.</summary>
        public SshKeyData SshKey { get; set; }
        // Stored in UTC: sync compares these across devices and time zones. Older vaults hold local
        // times with an offset (or without one), which are converted when read.
        public DateTime LastModifiedTime { get; set => field = ToUtc(value); }
        public DateTime CreationTime { get; set => field = ToUtc(value); }

        private static DateTime ToUtc(DateTime value) =>
            value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();

        public Credential() { }

        public static Credential CreateNew(CredentialType type = CredentialType.Login)
        {
            var credential = new Credential
            {
                Id = Guid.NewGuid(),
                Type = type,
                SshKey = type == CredentialType.SshKey ? new SshKeyData() : null,
            };
            return credential;
        }

        internal Credential Clone()
        {
            var clone = new Credential
            {
                Id = Id,
                Type = Type,
                NameField = NameField.Clone(),
                LoginField = LoginField.Clone(),
                PasswordField = PasswordField.Clone(),
                PasswordHistory = PasswordHistory,
                OtherField = OtherField.Clone(),
                SiteField = SiteField.Clone(),
                Totp = Totp,
                SshKey = SshKey?.Clone(),
                LastModifiedTime = LastModifiedTime,
                CreationTime = CreationTime
            };

            foreach (var field in AdditionalFields)
            {
                var fieldClone = field.Clone();
                clone.AdditionalFields.Add(fieldClone);
            }
            return clone;
        }

        public override bool Equals(object obj)
        {
            return obj is Credential credential &&
                   Id.Equals(credential.Id);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id);
        }
    }
}
