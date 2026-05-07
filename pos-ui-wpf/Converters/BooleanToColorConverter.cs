using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // When toggle is ON (true), return light grey (disabled look)
                // When toggle is OFF (false), return black (enabled look)
              return isActive ? "#CCCCCC" : "#000000";
            }
            return new SolidColorBrush(Colors.Black); // Default color
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}