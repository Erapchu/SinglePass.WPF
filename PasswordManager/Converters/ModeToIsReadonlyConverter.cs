using SinglePass.WPF.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    [ValueConversion(typeof(CredentialsDialogMode), typeof(bool))]
    internal class ModeToIsReadonlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CredentialsDialogMode mode)
            {
                switch (mode)
                {
                    case CredentialsDialogMode.New:
                        return false;
                    case CredentialsDialogMode.View:
                        return true;
                    case CredentialsDialogMode.Edit:
                        return false;
                    default:
                        break;
                }
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
