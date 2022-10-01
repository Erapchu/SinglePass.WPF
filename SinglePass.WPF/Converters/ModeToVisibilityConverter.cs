using SinglePass.WPF.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    [ValueConversion(typeof(CredentialDetailsMode), typeof(Visibility))]
    internal class ModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CredentialDetailsMode mode)
            {
                var inverse = parameter is string sparam && sparam == "inverse";

                switch (mode)
                {
                    case CredentialDetailsMode.New:
                    case CredentialDetailsMode.Edit:
                        return inverse ? Visibility.Collapsed : Visibility.Visible;
                    case CredentialDetailsMode.View:
                        return inverse ? Visibility.Visible : Visibility.Collapsed;
                    default:
                        break;
                }
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
