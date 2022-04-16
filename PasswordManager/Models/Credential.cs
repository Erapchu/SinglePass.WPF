using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PasswordManager.Models
{
    [DebuggerDisplay("{NameField.Value}")]
    public class Credential
    {
        public Guid Id { get; init; }
        public PassField NameField { get; set; } = new PassField() { Name = "Name" };
        public PassField LoginField { get; set; } = new PassField() { Name = "Login" };
        public PassField PasswordField { get; set; } = new PassField() { Name = "Password" };
        public PassField OtherField { get; set; } = new PassField() { Name = "Other" };
        public PassField SiteField { get; set; } = new PassField() { Name = "Site" };
        public List<PassField> AdditionalFields { get; set; } = new List<PassField>();
        public DateTime LastModifiedTime { get; set; }
        public DateTime CreationTime { get; set; }

        public Credential() { }

        public static Credential CreateNew()
        {
            var credential = new Credential
            {
                Id = Guid.NewGuid()
            };
            return credential;
        }

        internal Credential Clone()
        {
            var clone = new Credential
            {
                Id = Id,
                NameField = NameField.Clone(),
                LoginField = LoginField.Clone(),
                PasswordField = PasswordField.Clone(),
                OtherField = OtherField.Clone(),
                SiteField = SiteField.Clone(),
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
