using SinglePass.WPF.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    [ValueConversion(typeof(CredentialDetailsMode), typeof(bool))]
    internal class ModeToIsReadonlyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CredentialDetailsMode mode)
            {
                switch (mode)
                {
                    case CredentialDetailsMode.New:
                    case CredentialDetailsMode.Edit:
                        return false;
                    case CredentialDetailsMode.View:
                        return true;
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
