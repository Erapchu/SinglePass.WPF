using System;
using System.Globalization;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    public sealed class PasswordMaskConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int length ? new string('●', length) : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
