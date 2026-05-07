using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using POS_UI.Models;

namespace POS_UI.Converters
{
    public class OrderTypeToBadgeBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Default: light gray
            var fallback = (SolidColorBrush)new BrushConverter().ConvertFromString("#FFE6E6E6");

            if (value is OrderType orderType)
            {
                switch (orderType)
                {
                    case OrderType.DineIn:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#379ae6"));
                    case OrderType.TakeAway:
                    case OrderType.Collection:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009230"));
                    case OrderType.Delivery:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff9400"));
                    default:
                        return fallback;
                }
            }

            // Support table order method (e.g. from OrderModel.TableOrderMethod)
            if (value is string TableOrderMethod)
            {
                TableOrderMethod = TableOrderMethod?.Trim().ToUpperInvariant() ?? "";
                // DINE-IN: same blue as OrderType.DineIn
                if (TableOrderMethod == "DINE-IN" || TableOrderMethod == "DINE IN" || TableOrderMethod == "DINEIN")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#379ae6"));
                // Take Away / Takeaway: same green as OrderType.TakeAway
                if (TableOrderMethod == "TAKEAWAY" || TableOrderMethod == "TAKE AWAY")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009230"));
                if (TableOrderMethod == "COLLECTION")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009230"));
                if (TableOrderMethod == "DELIVERY")
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff9400"));
                return fallback;
            }

            // Also support string values just in case
            if (value is string s)
            {
                s = s.Trim().ToUpperInvariant();
                if (s == "DINEIN" || s == "DINE IN") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#379ae6"));
                if (s == "TAKEAWAY" || s == "TAKE AWAY") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009230"));
                if (s == "COLLECTION") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#009230"));
                if (s == "DELIVERY") return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ff9400"));
            }

            return fallback;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


