using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for AddCustomerDialog.xaml
    /// </summary>
    public partial class AddCustomerDialog : OptimizedDialogBase
    {
        public AddCustomerDialog()
        {
            InitializeComponent();
        }

        private void CountryCodeArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only handle when dropdown is closed, so selection clicks in the popup work normally
            if (CountryCodeCombo != null && !CountryCodeCombo.IsDropDownOpen)
            {
                CountryCodeCombo.Focus();
                CountryCodeCombo.IsDropDownOpen = true;
                e.Handled = true;
            }
        }
    }
}
