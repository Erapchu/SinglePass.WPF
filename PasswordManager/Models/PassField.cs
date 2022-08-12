using System.Diagnostics;

namespace SinglePass.WPF.Models
{
    [DebuggerDisplay("{Name} - {Value}")]
    public class PassField
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public PassField Clone()
        {
            var clone = new PassField()
            {
                Name = Name,
                Value = Value,
            };
            return clone;
        }
    }
}
