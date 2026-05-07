using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null)
                return string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
            string stringValue = value?.ToString() ?? string.Empty;
            string param = parameter.ToString();
            // Support multiple values separated by |
            var options = param.Split('|');
            foreach (var option in options)
            {
                if (string.Equals(stringValue, option.Trim(), StringComparison.OrdinalIgnoreCase))
                    return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 