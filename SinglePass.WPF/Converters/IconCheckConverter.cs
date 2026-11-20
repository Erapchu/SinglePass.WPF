using SinglePass.WPF.Extensions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SinglePass.WPF.Converters
{
    [ValueConversion(typeof(ImageSource), typeof(ImageSource))]
    public class IconCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null && !DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                // Assembly location is unavailable in single-file apps, so take the icon from the process executable.
                var processPath = Environment.ProcessPath;
                if (processPath is null)
                    return value;

                using var icon = Icon.ExtractAssociatedIcon(processPath);
                using var bitmap = icon?.ToBitmap();
                return bitmap?.GetWpfImageSource();
            }
            else
            {
                return value;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
