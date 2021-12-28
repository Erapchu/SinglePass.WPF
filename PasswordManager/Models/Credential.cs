using System;
using System.Collections.Generic;

namespace PasswordManager.Models
{
    public class Credential
    {
        public Guid Id { get; set; }
        public PassField NameField { get; set; }
        public PassField LoginField { get; set; }
        public PassField PasswordField { get; set; }
        public PassField OtherField { get; set; }
        public List<PassField> AdditionalFields { get; set; }

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
            foreach (var field in AdditionalFields)
            {
                var fieldClone = field.Clone();
                clone.AdditionalFields.Add(fieldClone);
            }
            return clone;
        }
    }
}
