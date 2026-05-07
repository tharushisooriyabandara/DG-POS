using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;
using POS_UI.ViewModels;

namespace POS_UI.Converters
{
    public class ItemEditableToHitTestVisibleConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2) return true;

            var item = values[0] as OrderItem;
            var viewModel = values[1] as CashierHomeViewModel;

            if (item == null || viewModel == null) return true;

            // If the item is editable, allow interaction (true)
            // If the item is not editable, disable interaction (false)
            return viewModel.IsItemEditable(item);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
