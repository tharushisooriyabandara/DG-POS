using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Generic;
using System.Linq;

namespace POS_UI.Converters
{
    public class ModifierItemSelectionConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 3 && values[0] is Dictionary<int, List<string>> selectedItems &&
                values[1] is int modifierId && values[2] is string itemName)
            {
                bool isSelected = selectedItems.TryGetValue(modifierId, out List<string> selected) && 
                                 selected != null && selected.Contains(itemName);
                
                if (targetType == typeof(Brush))
                {
                    if (parameter as string == "Foreground")
                        return isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(25, 118, 210));
                    else
                        return isSelected ? new SolidColorBrush(Color.FromRgb(25, 118, 210)) : Brushes.White;
                }
                return isSelected;
            }
            return targetType == typeof(Brush) ? Brushes.White : (object)false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 