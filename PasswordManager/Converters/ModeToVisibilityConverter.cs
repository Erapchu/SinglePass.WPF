using PasswordManager.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PasswordManager.Converters
{
    [ValueConversion(typeof(CredentialsDialogMode), typeof(Visibility))]
    internal class ModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CredentialsDialogMode mode)
            {
                var inverse = parameter is string sparam && sparam == "inverse";

                switch (mode)
                {
                    case CredentialsDialogMode.New:
                        return inverse ? Visibility.Collapsed : Visibility.Visible;
                    case CredentialsDialogMode.View:
                        return inverse ? Visibility.Visible : Visibility.Collapsed;
                    case CredentialsDialogMode.Edit:
                        return inverse ? Visibility.Collapsed : Visibility.Visible;
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
