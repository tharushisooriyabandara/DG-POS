using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace POS_UI.Converters
{
    public class StringToForegroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string currentPage && parameter is string targetPage)
            {
                return currentPage == targetPage ? new SolidColorBrush(Colors.White) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 