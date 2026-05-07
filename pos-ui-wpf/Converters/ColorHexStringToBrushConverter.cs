using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class ColorHexStringToBrushConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string s || string.IsNullOrWhiteSpace(s))
                return new SolidColorBrush(Colors.LightGray);
            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(s);
                return new SolidColorBrush(color);
            }
            catch
            {
                return new SolidColorBrush(Colors.LightGray);
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
