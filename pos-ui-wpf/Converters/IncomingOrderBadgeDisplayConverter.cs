using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    /// <summary>Caps the displayed integer (e.g. sidebar badge) while the source count stays uncapped.</summary>
    public class IncomingOrderBadgeDisplayConverter : IValueConverter
    {
        private const int MaxDisplay = 5;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int n = 0;
            if (value is int i)
                n = i;
            else if (value != null && int.TryParse(System.Convert.ToString(value, culture), NumberStyles.Integer, culture, out int parsed))
                n = parsed;
            return Math.Min(n, MaxDisplay).ToString(culture);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
