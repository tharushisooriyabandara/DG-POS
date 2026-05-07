using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.Converters
{
    public class DiscountLineAmountConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
            {
                return string.Empty;
            }

            var currency = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";

            decimal discountAmount = 0m;
            try
            {
                if (values[0] != null)
                {
                    discountAmount = System.Convert.ToDecimal(values[0], CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                return string.Empty;
            }

            int quantity = 1;
            try
            {
                if (values.Length > 1 && values[1] != null)
                {
                    quantity = System.Convert.ToInt32(values[1], CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                quantity = 1;
            }

            ProductItemModel product = null;
            if (values.Length > 2)
            {
                product = values[2] as ProductItemModel;
            }

            int platformId = 0;
            try
            {
                if (values.Length > 3 && values[3] != null)
                {
                    platformId = System.Convert.ToInt32(values[3], CultureInfo.InvariantCulture);
                }
            }
            catch
            {
                platformId = 0;
            }

            // For all orders, if quantity > 1, multiply the unit discount to show total line discount
            if (quantity > 1 && discountAmount != 0m)
            {
                discountAmount = Math.Round(discountAmount * quantity, 2, MidpointRounding.AwayFromZero);
            }

            return $"{currency} {discountAmount:F2}";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}


