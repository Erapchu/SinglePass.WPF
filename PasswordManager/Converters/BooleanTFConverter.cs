using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PasswordManager.Converters
{
    public sealed class BooleanTFConverter : BooleanConverter<bool>
    {
        public BooleanTFConverter() : base(true, false) { }
    }
}
