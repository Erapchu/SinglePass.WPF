using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    public sealed class LocalizedFormatConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values[0] is string format && values[1] != DependencyProperty.UnsetValue
                ? string.Format(culture, format, values[1])
                : string.Empty;

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
