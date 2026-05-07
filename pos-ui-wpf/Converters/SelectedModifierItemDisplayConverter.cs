using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class SelectedModifierItemDisplayConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 &&
                values[0] is int modifierId &&
                values[1] is Dictionary<int, string> selectedItems &&
                values[2] is IEnumerable<ModifierItemModel> modifierItems)
            {
                if (selectedItems.TryGetValue(modifierId, out string selectedName))
                {
                    var item = modifierItems.FirstOrDefault(mi => mi.ItemName == selectedName);
                    if (item != null)
                    {
                        var currency = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";
                        return $"{item.ItemName}   {currency} {item.ItemPrice:0.00}";
                    }
                }
            }
            return string.Empty;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 