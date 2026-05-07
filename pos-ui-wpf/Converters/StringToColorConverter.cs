using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentPage && parameter is string targetPage)
            {
                return currentPage == targetPage ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1976D2")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F0F0"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 