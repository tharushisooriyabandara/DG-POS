using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    /// <summary>Two-way converter for binding TextBox.Text to an int. Invalid or empty string converts to default (e.g. 10).</summary>
    public class IntToTextConverter : IValueConverter
    {
        public int DefaultValue { get; set; } = 10;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i)
                return i.ToString(culture);
            return DefaultValue.ToString(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                return DefaultValue;
            if (value is string str && int.TryParse(str.Trim(), NumberStyles.Integer, culture, out int result))
                return result;
            return DefaultValue;
        }
    }
}
