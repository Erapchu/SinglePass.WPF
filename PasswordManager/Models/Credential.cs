using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;

namespace PasswordManager.Models
{
    public class Credential
    {
        public static Dictionary<string, PassField> DefaultFields => new()
        {
            { "Name", new PassField() { IconKind = PackIconKind.Information } },
            { "Login", new PassField() { IconKind = PackIconKind.Account } },
            { "Password", new PassField() { IconKind = PackIconKind.Key } },
            { "Other", new PassField() { IconKind = PackIconKind.InformationOutline } },
        };

        public Guid Id { get; set; }
        public Dictionary<string, PassField> Fields { get; set; }

        public Credential(bool useDefaultFields = true)
        {
            Id = Guid.NewGuid();
            Fields = useDefaultFields ? DefaultFields : new Dictionary<string, PassField>();
        }
    }
}
