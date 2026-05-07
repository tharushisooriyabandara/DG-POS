using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;

namespace POS_UI.Converters
{
    public class GuestCustomerToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is CustomerModel customer)
            {
                // Check if the customer name is "Guest Customer"
                bool isGuestCustomer = string.Equals(customer.Name, "Guest Customer", StringComparison.OrdinalIgnoreCase);
                
                // If parameter is "Invert", return the inverted value
                if (parameter?.ToString() == "Invert")
                {
                    return !isGuestCustomer;
                }
                
                return isGuestCustomer;
            }
            
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
