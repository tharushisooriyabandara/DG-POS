using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;

namespace POS_UI.Converters
{
    public class TableStatusToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TableStatus status)
            {
                return status == TableStatus.Available;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 