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
using System.ComponentModel;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for CheckOutDialog.xaml
    /// </summary>
    public partial class CheckoutDialog : OptimizedDialogBase
    {
        public CheckoutDialog()
        {
            InitializeComponent();
            this.DataContextChanged += CheckoutDialog_DataContextChanged;
        }

        private void CheckoutDialog_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Handle both CashierHomeViewModel and KitchenCheckoutViewModel
            if (e.NewValue is ViewModels.CashierHomeViewModel cashierViewModel)
            {
                cashierViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
            else if (e.NewValue is ViewModels.KitchenCheckoutViewModel kitchenViewModel)
            {
                kitchenViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Handle SelectedPaymentMethod property change for both ViewModels
            if (e.PropertyName == nameof(ViewModels.CashierHomeViewModel.SelectedPaymentMethod) ||
                e.PropertyName == nameof(ViewModels.KitchenCheckoutViewModel.SelectedPaymentMethod))
            {
                // Check if it's Cash payment for either ViewModel
                bool isCashSelected = false;
                
                if (sender is ViewModels.CashierHomeViewModel cashierViewModel)
                {
                    isCashSelected = cashierViewModel.SelectedPaymentMethod == ViewModels.PaymentMethod.Cash;
                }
                else if (sender is ViewModels.KitchenCheckoutViewModel kitchenViewModel)
                {
                    isCashSelected = kitchenViewModel.SelectedPaymentMethod == ViewModels.PaymentMethod.Cash;
                }
                
                if (isCashSelected)
                {
                    // Clear the cash input and focus the field when Cash payment is selected
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // Focus the textbox
                        CashGivenTextBox.Focus();

                        // Set keyboard focus
                        Keyboard.Focus(CashGivenTextBox);

                        // Select existing text so typing replaces it without clearing manually
                        CashGivenTextBox.SelectAll();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        //hamburger menu
        private void HamburgerMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }
    }
}
