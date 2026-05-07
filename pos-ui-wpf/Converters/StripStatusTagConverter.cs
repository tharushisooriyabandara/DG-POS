using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class StripStatusTagConverter : IValueConverter
    {
        private static readonly Regex StatusTagRegex = new Regex(@"\s*\[STATUS:\s*\w+\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = value as string;
            if (string.IsNullOrEmpty(text)) return value;
            var cleaned = StatusTagRegex.Replace(text, string.Empty);
            return cleaned?.Trim();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value;
        }
    }
}


