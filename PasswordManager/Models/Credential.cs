using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;

namespace PasswordManager.Models
{
    public class Credential
    {
        public static List<PassField> DefaultFields => new()
        {
            { new PassField() { Name = "Name", IconKind = PackIconKind.Information } },
            { new PassField() { Name = "Login", IconKind = PackIconKind.Account } },
            { new PassField() { Name = "Password", IconKind = PackIconKind.Key } },
            { new PassField() { Name = "Other", IconKind = PackIconKind.InformationOutline } },
        };

        public Guid Id { get; set; }
        public List<PassField> Fields { get; set; }

        public Credential(bool useDefaultFields = true)
        {
            Id = Guid.NewGuid();
            Fields = useDefaultFields ? DefaultFields : new List<PassField>();
        }

        internal Credential Clone()
        {
            var clone = new Credential(false);
            clone.Id = Id;
            foreach (var field in Fields)
            {
                var fieldClone = field.Clone();
                clone.Fields.Add(fieldClone);
            }
            return clone;
        }
    }
}
