using System;
using System.Globalization;
using System.Windows.Data;

namespace POS_UI.Converters
{
    public class RoleToForgotTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string role)
            {
                if (role == "Admin" || role == "Outlet Admin")
                {
                    return "Forgot PIN Code?";
                }
                else
                {
                    return "Forgot PIN Code?";
                }
            }
            
            // Default to PIN code if no role is specified
            return "Forgot PIN Code?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
