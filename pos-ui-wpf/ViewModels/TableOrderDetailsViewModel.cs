using POS_UI.Models;
using POS_UI.Services;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class TableOrderDetailsViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private OrderModel _order;
        private bool _isLoading;
       
        
        public OrderModel Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(OrderNumber));
                OnPropertyChanged(nameof(CustomerName));
                OnPropertyChanged(nameof(OrderType));
                OnPropertyChanged(nameof(TableNumber));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(SubTotal));
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        // Properties for UI binding
        public string OrderNumber => Order?.OrderNumber ?? "";
        public string CustomerName => Order?.CustomerName ?? "";
        public string OrderType => Order?.OrderType.ToString() ?? "";
        public string TableNumber => Order?.TableNumber?.ToString() ?? "";
        public decimal Total => Order?.ApiSubTotal ?? 0m;
        public decimal SubTotal => Order?.ApiTotal ?? 0m;
        public decimal Discount => Order?.DiscountAmount ?? 0m;
        public string DiscountDescription 
        {
            get
            {
                if (Order == null) return "Discount";
                
                // Only show discount description for POS orders (platform_id != 1, 2, 6)
                // Platform 1 = Uber, 2 = Deliveroo, 6 = Webshop
                if (Order.PlatformId == 1 || Order.PlatformId == 2 || Order.PlatformId == 6)
                {
                    return "Discount"; // Keep original for delivery platforms
                }
                
                // For POS orders, show discount mode
                if (Order.DiscountModeApplied == "percentage" && Order.DiscountPercentage > 0)
                {
                    return $"Discount ({Order.DiscountPercentage}%)";
                }
                else if (Order.DiscountModeApplied == "value" && Order.DiscountAmount > 0)
                {
                    return "Discount (value)";
                }
                else
                {
                    return "Discount";
                }
            }
        }
        public bool HasDiscount => Discount > 0 || (Order?.DiscountPercentage > 0);
        

        public ICommand UpdateOrderCommand => new RelayCommand(UpdateOrder);
        public ICommand PrintCommand => new RelayCommand(PrintOrder);


        public event Action<OrderModel> UpdateOrderRequested;

        public TableOrderDetailsViewModel() 
        {
            _apiService = new ApiService();
        }
        
        public TableOrderDetailsViewModel(OrderModel order)
        {
            _apiService = new ApiService();
            Order = order;
        }

        public TableOrderDetailsViewModel(int orderId)
        {
            _apiService = new ApiService();
            _ = LoadOrderDetailsAsync(orderId);
        }

        private async Task LoadOrderDetailsAsync(int orderId)
        {
            try
            {
                IsLoading = true;
                var orderDetails = await _apiService.GetOrderByIdAsync(orderId);
                Order = orderDetails;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error loading order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateOrder()
        {
            UpdateOrderRequested?.Invoke(Order);
        }

        private void PrintOrder()
        {
            try
            {
                if (Order == null)
                {
                    MessageBox.Show("No order available to print.", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string paymentMethod = null;
                if (!string.IsNullOrWhiteSpace(Order.PaymentMethod))
                {
                    paymentMethod = Order.PaymentMethod;
                }
                else if (!string.IsNullOrWhiteSpace(Order.PaymentStatus))
                {
                    paymentMethod = Order.PaymentStatus;
                }
                else if (Order.IsPaid)
                {
                    paymentMethod = "PAID";
                }

                _ = ReceiptPrintingService.Instance.PrintIncomingOrderReceiptAsync(Order, paymentMethod);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Error printing order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        
    }
} 