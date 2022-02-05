using System;
using System.Globalization;
using System.Windows.Data;

namespace PasswordManager.Converters
{
    [ValueConversion(typeof(int), typeof(bool))]
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueInt = (int)value;
            var parameterInt = (int)parameter;

            return valueInt == parameterInt;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return parameter;
        }
    }
}
