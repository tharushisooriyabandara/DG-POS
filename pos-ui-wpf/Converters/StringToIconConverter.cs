using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class StringToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                return text == "Table" ? "Table" : "Clock";
            }
            return "Clock";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 