using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using POS_UI.Models;
using System.Linq;

namespace POS_UI.Converters
{
    public class TableStatusVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not TableStatus status || parameter is not string targetStatus)
            {
                return Visibility.Collapsed;
            }

            
            string[] targetStatuses = targetStatus.Split(',');

            return targetStatuses.Any(s => s.Trim().Equals(status.ToString(), StringComparison.OrdinalIgnoreCase))
                ? Visibility.Visible
                : Visibility.Collapsed;
            //return status.ToString() == targetStatus ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 