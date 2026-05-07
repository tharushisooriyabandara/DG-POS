using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.ViewModels;

namespace POS_UI.Converters
{
    public class SortOptionToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProductSortOption option)
            {
                switch (option)
                {
                    case ProductSortOption.None: return "Sort: None";
                    case ProductSortOption.AZ: return "A - Z";
                    case ProductSortOption.ZA: return "Z - A";

                    case ProductSortOption.PriceLowHigh: return "Low - High";
                    case ProductSortOption.PriceHighLow: return "High - Low";
                    default: return "Sort: None";

                }
            }
            return "Sort: None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 