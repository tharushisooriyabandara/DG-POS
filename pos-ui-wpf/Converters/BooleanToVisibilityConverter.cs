using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            if (parameter != null)
            {
                if (parameter.ToString().ToLower() == "invert")
                {
                    boolValue = !boolValue;
                }
                else if (bool.TryParse(parameter.ToString(), out bool param))
                {
                    boolValue = boolValue == param;
                }
            }
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value is Visibility v && v == Visibility.Visible);
        }
    }
} 