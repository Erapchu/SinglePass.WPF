using MaterialDesignThemes.Wpf;

namespace PasswordManager.Models
{
    public class PassField
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public PackIconKind IconKind { get; set; }

        public PassField Clone()
        {
            var clone = new PassField()
            {
                Name = Name,
                Value = Value,
                IconKind = IconKind
            };
            return clone;
        }
    }
}
