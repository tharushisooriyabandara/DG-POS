using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class SeatCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int seatCount)
            {
                return seatCount == 1 ? "1 seat" : $"{seatCount} seats";
            }
            return "0 seats";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}