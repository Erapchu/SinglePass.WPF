using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PasswordManager.Models
{
    [DebuggerDisplay("{NameField.Value}")]
    public class Credential
    {
        public Guid Id { get; set; }
        public PassField NameField { get; set; }
        public PassField LoginField { get; set; }
        public PassField PasswordField { get; set; }
        public PassField OtherField { get; set; }
        public List<PassField> AdditionalFields { get; set; }
        public DateTime LastModifiedTime { get; set; }
        public DateTime CreationTime { get; set; }

        public Credential()
        {
            Id = Guid.NewGuid();
            NameField = new PassField() { Name = "Name" };
            LoginField = new PassField() { Name = "Login" };
            PasswordField = new PassField() { Name = "Password" };
            OtherField = new PassField() { Name = "Other" };
            AdditionalFields = new List<PassField>();
        }

        internal Credential Clone()
        {
            var clone = new Credential();
            clone.Id = Id;
            clone.NameField = NameField.Clone();
            clone.LoginField = LoginField.Clone();
            clone.PasswordField = PasswordField.Clone();
            clone.OtherField = OtherField.Clone();
            clone.LastModifiedTime = LastModifiedTime;
            clone.CreationTime = CreationTime;

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
