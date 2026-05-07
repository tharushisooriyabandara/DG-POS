using System;
using System.Globalization;
using System.Windows.Data;
using System.Collections.Generic;
using System.Linq;

namespace POS_UI.Converters
{
    public class ModifierItemEnabledConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 4 && values[0] is Dictionary<int, List<string>> selectedItems &&
                values[1] is int modifierId && values[2] is string itemName && values[3] is int maxPermitted)
            {
                var currentSelections = selectedItems.TryGetValue(modifierId, out List<string> selected) ? selected : new List<string>();
                var isCurrentlySelected = currentSelections.Contains(itemName);
                
                // For radio button behavior (min=1, max=1), all items should always be enabled
                // This allows users to change their selection easily
                if (maxPermitted == 1)
                {
                    return true;
                }
                
                // If item is already selected, it can always be deselected (for non-radio cases)
                if (isCurrentlySelected)
                {
                    return true;
                }
                
                // If we haven't reached max, allow selection
                if (maxPermitted == 0 || currentSelections.Count < maxPermitted)
                {
                    return true;
                }
                
                return false;
            }
            return true;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 