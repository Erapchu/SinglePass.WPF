using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PasswordManager.Converters
{
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class TransitionVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int valueInt && parameter is int parameterInt
                ? valueInt == parameterInt ? Visibility.Visible : Visibility.Collapsed
                : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
