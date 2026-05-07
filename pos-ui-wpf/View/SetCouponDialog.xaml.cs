using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using POS_UI.Services;
using POS_UI.ViewModels;
using System.Collections.Generic;
using POS_UI.Models;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for SetCouponDialog.xaml
    /// </summary>
    public partial class SetCouponDialog : UserControl
    {
        public SetCouponDialog()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
        }

        /*
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var couponCode = CouponTextBox.Text?.Trim();
            MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(couponCode, null);
        }
        */
        private async void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Disable the validate button to prevent multiple clicks
                ValidateButton.IsEnabled = false;
                ValidateButton.Content = "Saving..";
                
                var voucher = CouponTextBox.Text?.Trim();
                if (string.IsNullOrWhiteSpace(voucher))
                {
                    //MessageBox.Show("Please enter a coupon code.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateInfo("Invalid Coupon", "Please enter a coupon code.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };

                    // Close the current dialog first, then show the success message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show success message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    
                    // Re-enable the button
                    ValidateButton.IsEnabled = true;
                    ValidateButton.Content = "Save";
                    return;
                }

                var vm = DataContext as CashierHomeViewModel;
                if (vm == null)
                {
                    //MessageBox.Show("Error: Unable to access order data.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Accessing Order Data", "Error: Unable to access order data.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };
                   // Close the current dialog first, then show the success message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show success message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }

                var cart = CartService.Instance;
                if (cart == null)
                {
                    //MessageBox.Show("Error: Unable to access cart data.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error Accessing Cart Data", "Error: Unable to access cart data.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };
                    // Close the current dialog first, then show the success message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show success message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    return;
                }

                var purchaseType = vm.OrderType switch
                {
                    "Take Away" => "COLLECTION",
                    "Dine In" => "COLLECTION",
                    "Delivery" => "DELIVERY",
                    _ => "COLLECTION"
                };

                var paymentType = vm.SelectedPaymentMethod.ToString().ToUpper();

                var customerId = vm.SelectedCustomer.CustomerId;
                //MessageBox.Show($"CustomerId: {customerId}");
                var settings = new SettingsService().LoadSettings();
                var brandId = settings.Item3 ?? string.Empty;
                
                // Get shop ID from GlobalDataService instead of outlet code from settings
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                var outletId = shopDetails?.Id.ToString() ?? string.Empty;
                
                if (string.IsNullOrEmpty(outletId))
                {
                    MessageBox.Show("Error: Shop details not available. Please try logging in again.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var api = new ApiService();
                

                // Get category IDs from cart items (only the items that are actually in the cart)
                var categoryIds = new List<int>();
                foreach (var item in cart.OrderItems)
                {
                    Console.WriteLine($"Item: {item.Name}, Product: {item.Product?.ItemName}, CategoryId: {item.Product?.CategoryId}");
                    if (item.Product?.CategoryId > 0)
                    {
                        categoryIds.Add(item.Product.CategoryId);
                        Console.WriteLine($"Added CategoryId: {item.Product.CategoryId}");
                    }
                }
                
                var distinctCategoryIds = categoryIds.Distinct().ToArray();
                Console.WriteLine($"Total distinct category IDs being sent: [{string.Join(", ", distinctCategoryIds)}]");
               // var message = $"Cart SubTotal: {cart.SubTotal}, Cart Total: {cart.Total}";
               // MessageBox.Show(message, "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                

                // Calculate cart value after discount for coupon validation and calculation
                var cartValueAfterDiscount = cart.Total - cart.DiscountAmount;
                if (cartValueAfterDiscount < 0) cartValueAfterDiscount = 0;
                
                var result = await api.ValidateVoucherAsync(
                    voucher: voucher,
                    cartValue: cartValueAfterDiscount,
                    purchaseType: purchaseType,
                    outletId: outletId,
                    brandId: brandId,
                    categoryIds: distinctCategoryIds,
                    paymentType: paymentType,
                    customerId: customerId
                );
                
                var isValid = result.IsValid;
                var errorMessage = result.ErrorMessage;
                var voucherValue = result.VoucherValue;
                var valueType = result.ValueType;

                if (isValid)
                {
                    // Calculate coupon amount based on voucher value and type
                    // Apply coupon to the amount after discount (subtotal - discount amount)
                    decimal couponAmount = 0;
                    string couponDescription = $"Coupon ({voucher})";
                    
                    if (!string.IsNullOrEmpty(voucherValue) && decimal.TryParse(voucherValue, out decimal value))
                    {
                        if (valueType?.ToLower() == "percentage")
                        {
                            // Percentage discount - apply to subtotal after discount
                            couponAmount = Math.Round(cartValueAfterDiscount * value / 100m, 2, MidpointRounding.AwayFromZero);
                            couponDescription = $"Coupon ({voucher}) - {value}%";
                        }
                        else
                        {
                            // Fixed amount discount
                            couponAmount = value;
                            couponDescription = $"Coupon ({voucher}) - {value:C}";
                        }
                    }
                    
                    // Create voucher model
                    var voucherModel = new Models.VoucherModel
                    {
                        VoucherCode = voucher,
                        VoucherValue = voucherValue ?? string.Empty,
                        ValueType = valueType ?? string.Empty,
                        VoucherDiscount = couponAmount,
                        Validation = new List<object>(),
                        PurchaseType = purchaseType,
                        PaymentType = paymentType,
                        ValidCategories = distinctCategoryIds.Select(id => id.ToString()).ToList()
                    };
                    
                    // Apply voucher to cart
                    cart.ApplyVoucher(voucherModel);
                    
                    // Set the coupon details in the ViewModel
                    // These property setters will automatically trigger OnPropertyChanged notifications
                    vm.CouponCode = voucher;
                    vm.CouponAmount = couponAmount;
                    vm.CouponDescription = couponDescription;
                    
                    // Explicitly trigger property change notifications to ensure UI updates
                    /*vm.OnPropertyChanged(nameof(vm.HasCoupon));
                    vm.OnPropertyChanged(nameof(vm.CouponDescriptionWithAmount));
                    vm.OnPropertyChanged(nameof(vm.CouponDiscount));
                    vm.OnPropertyChanged(nameof(vm.SubTotal));*/
                    
                    //MessageBox.Show($"Coupon is valid! Discount: {couponAmount:C}", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                    var currency = GlobalDataService.Instance.ShopDetails?.Currency ?? "";
                    var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Valid Coupon", $"Coupon is valid! Discount: {currency}{couponAmount:F2}");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };
                    
                    // Close the current dialog first, then show the success message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show success message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                }
                else
                {
                    //MessageBox.Show(errorMessage, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    var vm1 = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Coupon", errorMessage);
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm1 };
                    
                    // Close the current dialog first, then show the error message
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                    });
                    
                    // Wait a moment for the dialog to close, then show error message
                    await Task.Delay(100);
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");
                    
                    // Re-enable the button for error case
                    ValidateButton.IsEnabled = true;
                    ValidateButton.Content = "Save";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error validating coupon: {ex.Message}", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Invalid Coupon",$"Coupon Validation Failed {ex.Message }");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                
                // Close the current dialog first, then show the error message
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("AddItemDialogHost", null);
                });
                
                // Wait a moment for the dialog to close, then show error message
                await Task.Delay(100);
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "AddItemDialogHost");

                // Re-enable the button for exception case
                ValidateButton.IsEnabled = true;
                ValidateButton.Content = "Save";
            }
        }
    }
}
