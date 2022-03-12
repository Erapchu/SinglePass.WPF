using PasswordManager.Extensions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;
using System.Windows.Media;

namespace PasswordManager.Converters
{
    [ValueConversion(typeof(ImageSource), typeof(ImageSource))]
    public class IconCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is null && !DesignerProperties.GetIsInDesignMode(new System.Windows.DependencyObject()))
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly is null)
                    return value;

                var icon = Icon.ExtractAssociatedIcon(entryAssembly.ManifestModule.FullyQualifiedName);
                return icon?.ToBitmap()?.GetWpfImageSource();
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
