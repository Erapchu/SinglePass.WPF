using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Erapkey.Core.Models
{
    /// <summary>
    /// Vault entry. Holds the fields every entry type has; the data of the type itself is in one block
    /// (<see cref="Login"/>, <see cref="SshKey"/>, <see cref="Card"/>, <see cref="Host"/>), and the blocks of other types are null.
    /// A new type adds a value to <see cref="CredentialType"/>, a block here and a line in
    /// <see cref="EnsureTypeData"/>. Merge, tombstones, groups and backups do not depend on the type.
    /// </summary>
    [DebuggerDisplay("{NameField.Value}")]
    public class Credential
    {
        public Guid Id { get; init; }
        public CredentialType Type { get; set; }
        public PassField NameField { get; set; } = new PassField() { Name = "Name" };
        public PassField OtherField { get; set; } = new PassField() { Name = "Other" };
        public List<PassField> AdditionalFields { get; set; } = new List<PassField>();

        /// <summary>Set for <see cref="CredentialType.Login"/> entries only.</summary>
        public LoginData Login { get; set; } = new LoginData();

        /// <summary>Set for <see cref="CredentialType.SshKey"/> entries only.</summary>
        public SshKeyData SshKey { get; set; }

        /// <summary>Set for <see cref="CredentialType.Card"/> entries only.</summary>
        public CardData Card { get; set; }

        /// <summary>Set for <see cref="CredentialType.Host"/> entries only.</summary>
        public HostData Host { get; set; }

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
            };
            credential.EnsureTypeData();
            return credential;
        }

        /// <summary>Creates the block of the entry's type if it is missing and drops the blocks of other types.</summary>
        internal void EnsureTypeData()
        {
            Login = Type == CredentialType.Login ? Login ?? new LoginData() : null;
            SshKey = Type == CredentialType.SshKey ? SshKey ?? new SshKeyData() : null;
            Card = Type == CredentialType.Card ? Card ?? new CardData() : null;
            Host = Type == CredentialType.Host ? Host ?? new HostData() : null;
        }

        internal Credential Clone()
        {
            var clone = new Credential
            {
                Id = Id,
                Type = Type,
                NameField = NameField.Clone(),
                OtherField = OtherField.Clone(),
                Login = Login?.Clone(),
                SshKey = SshKey?.Clone(),
                Card = Card?.Clone(),
                Host = Host?.Clone(),
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
