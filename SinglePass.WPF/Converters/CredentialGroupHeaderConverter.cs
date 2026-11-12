using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    public sealed class CredentialGroupHeaderConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
            values[0] switch
            {
                string initial => initial,
                DateTime month when values[1] is CultureInfo language => month.ToString("Y", language),
                null => values[2],
                _ => DependencyProperty.UnsetValue
            };

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
