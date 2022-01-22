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
                switch (mode)
                {
                    case CredentialsDialogMode.New:
                        return Visibility.Visible;
                    case CredentialsDialogMode.View:
                        return Visibility.Collapsed;
                    case CredentialsDialogMode.Edit:
                        return Visibility.Visible;
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
