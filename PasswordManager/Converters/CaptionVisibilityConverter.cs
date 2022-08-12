using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SinglePass.WPF.Converters
{
    public class CaptionVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values != null && values.Length == 3)
                {
                    var designMode = (bool)values[0];
                    var windowStyle = (WindowStyle)values[1];
                    var customVisibility = (Visibility)values[2];

                    if (designMode)
                        return Visibility.Collapsed;

                    if (windowStyle == WindowStyle.None)
                        return Visibility.Collapsed;

                    return customVisibility;
                }
            }
            catch
            {
                // Use default value
            }

            return Visibility.Visible;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
