using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace POS_UI.Converters
{
    [ValueConversion(typeof(string), typeof(string))]
    public class TrimConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var input = value as string;
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Trim leading/trailing whitespace and collapse internal multiple spaces
            var trimmed = input.Trim();
            trimmed = Regex.Replace(trimmed, @"\s+", " ");
            return trimmed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}


