using POS_UI.Models;
using POS_UI.Services;
using System.Threading.Tasks;
using System.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using POS_UI.ViewModels;
using MaterialDesignThemes.Wpf;
using POS_UI;
using POS_UI.View;
using System.Text.Json;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Security.Claims;

namespace POS_UI.ViewModels
{
    public class KitchenOrderDetailsDialogViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly CartService _cartService;
        private OrderModel _order;
        private bool _isLoading;
        private DialogMode _dialogMode;
        private bool _isDimmed;
        private bool _shouldReopenTableOrderListAfterClose = true;
        
        public enum DialogMode
        {
            Kitchen,     // Shows Delay Order button
            Tables,      // Shows Finish Order and Update Order buttons
            ViewOnly,    // Shows no action buttons (for customer details view)
            LiveOrders   // Shows Complete (when paid) or Payment (when unpaid) for platform 9 only
        }
        
        public OrderModel Order
        {
            get => _order;
            set
            {
                _order = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrimaryActionText));
                OnPropertyChanged(nameof(TableButtonText));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(DiscountAmount));
                OnPropertyChanged(nameof(DiscountDescription));
                OnPropertyChanged(nameof(HasDiscount));
                OnPropertyChanged(nameof(VoucherAmount));
                OnPropertyChanged(nameof(HasVoucher));
                OnPropertyChanged(nameof(VoucherDescription));
                OnPropertyChanged(nameof(HasNote));
                OnPropertyChanged(nameof(OrderNote));
                OnPropertyChanged(nameof(CustomerPhone));
                OnPropertyChanged(nameof(HasCustomerPhone));
                OnPropertyChanged(nameof(ShowCustomerPhone));
                OnPropertyChanged(nameof(AddressDisplay));
                OnPropertyChanged(nameof(ShowAddress));
                OnPropertyChanged(nameof(ShowKitchenUpdateOrderButton));
                OnPropertyChanged(nameof(PaymentStatus));
                OnPropertyChanged(nameof(PaymentMethod));
                OnPropertyChanged(nameof(PaymentMode));
                OnPropertyChanged(nameof(ShippingMethodText));
                OnPropertyChanged(nameof(PlatformLogo));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(DeliveryPlatformName));
                OnPropertyChanged(nameof(ShowTablesUpdateOrderButton));
                OnPropertyChanged(nameof(LiveOrdersUpdateOrderButton));
                OnPropertyChanged(nameof(ShowDelayOrderButton));
                OnPropertyChanged(nameof(IsPaidForRefund));
                OnPropertyChanged(nameof(ShowRefundMenuItem));
                OnPropertyChanged(nameof(ShowTransactions));
                OnPropertyChanged(nameof(ShowHamburgerMenu));
                OnPropertyChanged(nameof(IsLiveOrdersMode));
                OnPropertyChanged(nameof(ShowCompleteButtonLiveOrders));
                OnPropertyChanged(nameof(ShowPaymentButtonLiveOrders));
                OnPropertyChanged(nameof(ShowreadyButtonLiveOrders));
                
                // Populate tax details from API order_taxes array
                OrderTaxesFromApi.Clear();
                if (_order?.TaxSummaryRows != null && _order.TaxSummaryRows.Count > 0)
                {
                    foreach (var tax in _order.TaxSummaryRows)
                    {
                        OrderTaxesFromApi.Add(tax);
                    }
                }
                OnPropertyChanged(nameof(OrderTaxesFromApi));
                OnPropertyChanged(nameof(HasOrderTaxesFromApi));
                
                UpdateOrderTaxSummary();
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool IsDimmed
        {
            get => _isDimmed;
            set { _isDimmed = value; OnPropertyChanged(); }
        }

        public string TableButtonText
        {
            get
            {
                if (Order == null) return string.Empty;

                // Check if platform_id is 8, then display table name
                if (Order.PlatformId2 == 8 || Order.PlatformName == "TABLE_ORDER")
                {
                    return !string.IsNullOrWhiteSpace(Order.TableName) ? $"Table {Order.TableName}" : $"Time {Order.DeliveryDateTime.Value.ToUniversalTime().ToString("hh:mm tt")}";
                }

                return Order.OrderType switch
                {
                    OrderType.DineIn => Order.TableNumber.HasValue ? $"Table {Order.TableName}" : "Tableee",
                    OrderType.TakeAway or OrderType.Delivery => Order.DeliveryDateTime.HasValue 
                        ? $"Time {Order.DeliveryDateTime.Value.ToUniversalTime().ToString("hh:mm tt")}" 
                        : "00:00",
                    _ => "00:01"
                };
            }
        }

        public decimal RefundBalance => Order?.RefundBalance ?? 0m;
        //Refund Order Properties
        public bool IsPaidForRefund
        {
            get
            {
                if (Order == null) return false;
                string paymentStatus = (Order.PaymentStatus ?? string.Empty).Trim().ToUpper();
                decimal refundBalance = Order.RefundBalance;
                int platformId2 = Order.PlatformId2;
                string orderStatus = Order.ApiStatus;
                // Check if payment status contains "PAID" (handles "PAID", "PAID - CARD", etc.)
                return (paymentStatus.StartsWith("PAID", StringComparison.OrdinalIgnoreCase) || Order.IsPaid == true) && (refundBalance > 0) 
                        && (platformId2 == 6 || (platformId2 == 8 && !IsPayHereStripeOrVerifonePayment(Order)) || platformId2 == 9)
                        && (!string.Equals(orderStatus, "CREATED", StringComparison.OrdinalIgnoreCase) && !string.Equals(orderStatus, "MISSED", StringComparison.OrdinalIgnoreCase));
            }
        }

        private static bool IsPayHereStripeOrVerifonePayment(OrderModel order)
        {
            if (order == null) return false;
            var pt = !string.IsNullOrWhiteSpace(order.SessionPaymentType) ? order.SessionPaymentType : order.PaymentType;
            pt = (pt ?? "").Trim();
            return string.Equals(pt, "stripe", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pt, "payhere", StringComparison.OrdinalIgnoreCase)
                || string.Equals(pt, "verifone", StringComparison.OrdinalIgnoreCase);
        }

        //Transaction Visibility Properties
        public bool ShowTransactions
        {
            get
            {
                if (Order == null) return false;
                int platformId2 = Order.PlatformId2;
                return platformId2 == 9 || platformId2 == 6 || platformId2 == 8;
            }
        }
        // Order Summary Properties
        public decimal SubTotal => Order?.ApiSubTotal ?? 0m;
        public decimal Total => Order?.ApiTotal ?? 0m;
        public decimal DiscountAmount => Order?.DiscountAmount ?? 0m;
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
        public bool HasDiscount => DiscountAmount > 0 || (Order?.DiscountPercentage > 0);
        public decimal VoucherAmount => Order?.VoucherDiscount ?? 0m;
        public bool HasVoucher => VoucherAmount > 0;
        public string VoucherDescription
        {
            get
            {
                if (Order == null || !HasVoucher) return "Voucher";
                
                // Try to get voucher information from Order.Vouchers
                if (Order.Vouchers != null && Order.Vouchers.Count > 0)
                {
                    var firstVoucher = Order.Vouchers.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.VoucherCode));
                    if (firstVoucher != null)
                    {
                        string voucherDescription = $"Coupon ({firstVoucher.VoucherCode})";
                        if (!string.IsNullOrEmpty(firstVoucher.VoucherValue) && decimal.TryParse(firstVoucher.VoucherValue, out decimal voucherValue))
                        {
                            if (firstVoucher.ValueType?.ToLower() == "percentage")
                            {
                                // Percentage discount
                                voucherDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue}%";
                            }
                            else
                            {
                                // Fixed amount discount
                                voucherDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue:C}";
                            }
                        }
                        return voucherDescription;
                    }
                }
                
                // Fallback to CouponCode if available
                if (!string.IsNullOrWhiteSpace(Order.CouponCode))
                {
                    return $"Coupon ({Order.CouponCode})";
                }
                
                return "Voucher";
            }
        }
        
        // Shop fees display (from order details API)
        public ObservableCollection<ShopFeeDisplayModel> ShopFeeRows { get; set; } = new ObservableCollection<ShopFeeDisplayModel>();
        public bool HasShopFees => ShopFeeRows.Count > 0;
        public decimal TotalShopFees => ShopFeeRows.Sum(f => f.Amount);
        public decimal DeliveryCharges => Order?.DeliveryCharge ?? 0m;
        public bool HasDeliveryCharges => DeliveryCharges > 0m;
        public ObservableCollection<TaxSummaryRow> OrderTaxSummaryRows { get; } = new ObservableCollection<TaxSummaryRow>();
        public bool HasOrderTaxSummary => OrderTaxSummaryRows.Count > 0;
        
        // Tax details from API order_taxes array
        public ObservableCollection<TaxSummaryRow> OrderTaxesFromApi { get; } = new ObservableCollection<TaxSummaryRow>();
        public bool HasOrderTaxesFromApi => OrderTaxesFromApi.Count > 0;
        
        public bool HasNote => !string.IsNullOrWhiteSpace(Order?.OrderNotes);
        public string OrderNote => Order?.OrderNotes ?? "";
        public string CustomerPhone => Order?.CustomerPhone ?? "";
        public bool HasCustomerPhone => !string.IsNullOrWhiteSpace(CustomerPhone);
        public bool ShowCustomerPhone
        {
            get
            {
                var name = Order?.CustomerName?.Trim();
                if (string.Equals(name, "Guest Customer", StringComparison.OrdinalIgnoreCase)) return false;
                return HasCustomerPhone;
            }
        }

        // Address display for delivery orders
        public string AddressDisplay
        {
            get
            {
				var address = Order?.DeliveryAddress;
				var address1 = Order?.DeliveryAddressLine1;
				var address2 = Order?.DeliveryAddressLine2;
                var city = Order?.DeliveryCity;
                var postcode = Order?.DeliveryPostcode;
                var flatNo = Order?.DeliveryFlatNo;
				
				// Prefer composed address from line parts when any are present
				var parts = new System.Collections.Generic.List<string>();
				if (!string.IsNullOrWhiteSpace(flatNo)) parts.Add(flatNo);
				if (!string.IsNullOrWhiteSpace(address1)) parts.Add(address1);
				if (!string.IsNullOrWhiteSpace(address2)) parts.Add(address2);
				if (!string.IsNullOrWhiteSpace(city)) parts.Add(city);
				if (!string.IsNullOrWhiteSpace(postcode)) parts.Add(postcode);

				if (parts.Count > 0)
				{
					return string.Join(", ", parts);
				}
				
				// Fallback to single address field
				if (!string.IsNullOrWhiteSpace(address))
				{
					return address;
				}
				
				// If nothing present and it's a delivery order, try to get address from incoming order
				if (Order?.ShippingMethod == "DELIVERY" && Order?.ShippingMethod == "COLLECTION")
				{
					var incoming = GetAddressFromIncomingOrder();
					return string.IsNullOrWhiteSpace(incoming) ? string.Empty : incoming;
				}
				
				return string.Empty;
            }
        }
        public bool ShowAddress
        {
            get
            {
				// Show only for delivery orders and when any address data exists
				if (Order?.ShippingMethod != "DELIVERY" && Order?.ShippingMethod != "COLLECTION") return false;
				if (!string.IsNullOrWhiteSpace(Order?.DeliveryAddress)) return true;
				if (!string.IsNullOrWhiteSpace(Order?.DeliveryAddressLine1)) return true;
				if (!string.IsNullOrWhiteSpace(Order?.DeliveryAddressLine2)) return true;
				return !string.IsNullOrWhiteSpace(AddressDisplay);
            }
        }

        //Transactions Properties
        public ObservableCollection<OrderTransactionModel> Transactions { get; set; } = new ObservableCollection<OrderTransactionModel>();
        // Header badges/display
        public string PaymentStatus
        {
            get
            {
                if (Order == null) return "";
                /*var total = Order.ApiTotal ?? 0m;
                var isUnpaid = string.Equals(Order.PaymentStatus,"UNPAID",StringComparison.OrdinalIgnoreCase);
                var balance = Order.RefundBalance;
                if (total > 0 && balance <= 0 && !isUnpaid)
                    return "Refunded";
                if (total > 0 && balance < total && !isUnpaid)
                    return "Partially Refunded";*/
                var refundStatus = Order.RefundStatus;
                return string.IsNullOrWhiteSpace(refundStatus) ? Order.PaymentStatus ?? "" : refundStatus;
            }
        }

        public bool IsRefundedStatus => PaymentStatus == "Refunded" || PaymentStatus == "Partially Refunded";

        public string PaymentMethod => Order?.PaymentMethod ?? "";
        public string PaymentMode => Order?.PaymentMode ?? "";
        /*public string PaymentMode 
        {
            get
            {
                if (Order == null) return "";
                
                // If platform ID is 6 (Webshop) and payment status is paid, display as CARD
                if (Order.PlatformId == 6 || Order.PlatformId2 == 6 && !string.IsNullOrWhiteSpace(Order.PaymentStatus) && 
                    Order.PaymentStatus.ToUpper().Contains("PAID"))
                {
                    return "CARD";
                }
                
                return Order.PaymentMode ?? "";
            }
        }*/
        public string ShippingMethodText
        {
            get
            {
                if (Order == null) return "N/A";
                
                // Check if platform is Table Order (PlatformId == 8 or PlatformId2 == 8)
                if (Order.PlatformId == 8 || Order.PlatformId2 == 8)
                {
                    return string.IsNullOrWhiteSpace(Order.TableOrderMethod) ? "N/A" : Order.TableOrderMethod.ToUpperInvariant();
                }
                
                return string.IsNullOrWhiteSpace(Order.ShippingMethod) ? "N/A" : Order.ShippingMethod;
            }
        }
        public string PlatformLogo => Order?.PlatformLogo;
        public string DeliveryPlatformName => Order?.DeliveryPlatfornName ?? "";

        // Commands
        public ICommand PrintCommand { get; set; }
        public ICommand PrimaryActionCommand { get; set; }
        public ICommand UpdateOrderCommand { get; set; }
        public ICommand FinishOrderCommand { get; set; }
        public ICommand CancelOrderCommand { get; set; }
        public ICommand DelayOrderCommand { get; set; }
        public ICommand RefundOrderCommand { get; set; }
        public ICommand OpenTransactionsCommand { get; set; }
        public ICommand CompleteOrderLiveOrdersCommand { get; set; }
        public ICommand PaymentOrderLiveOrdersCommand { get; set; }
        public ICommand ReadyOrderLiveOrdersCommand { get; set; }
        // Properties for button visibility
        public bool IsKitchenMode => _dialogMode == DialogMode.Kitchen;
        public bool IsTablesMode => _dialogMode == DialogMode.Tables;
        public bool IsViewOnlyMode => _dialogMode == DialogMode.ViewOnly;
        public bool IsLiveOrdersMode => _dialogMode == DialogMode.LiveOrders;
        public bool ShouldReopenTableOrderListAfterClose => _shouldReopenTableOrderListAfterClose;

        /// <summary>Visible in Live Orders mode when paid, or always for Uber/Deliveroo/Table orders after Ready.</summary>
        public bool ShowCompleteButtonLiveOrders
        {
            get
            {
                if (!IsLiveOrdersMode || Order == null) return false;
                var status = (Order.PaymentStatus ?? string.Empty).Trim().ToUpperInvariant();
                bool paid = status.StartsWith("PAID", StringComparison.OrdinalIgnoreCase) || Order.IsPaid == true;
                bool alwaysComplete = (Order.PlatformId2 == 1 || Order.PlatformId2 == 2)
                    && string.Equals(Order.ApiStatus, "READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase);
                return (paid || alwaysComplete) && !ShowreadyButtonLiveOrders;
            }
        }

        /// <summary>Visible only in Live Orders mode when unpaid (excluding Uber/Deliveroo/Table orders after Ready).</summary>
        public bool ShowPaymentButtonLiveOrders
        {
            get
            {
                if (!IsLiveOrdersMode || Order == null) return false;
                var status = (Order.PaymentStatus ?? string.Empty).Trim().ToUpperInvariant();
                bool paid = status.StartsWith("PAID", StringComparison.OrdinalIgnoreCase) || Order.IsPaid == true;
                bool alwaysComplete = (Order.PlatformId2 == 1 || Order.PlatformId2 == 2)
                    && string.Equals(Order.ApiStatus, "READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase);
                return !paid && !alwaysComplete && !ShowreadyButtonLiveOrders;
            }
        }

        //Visible only for non pos orders and not ready for pickup
        public bool ShowreadyButtonLiveOrders
        {
            get
            {
                if (!IsLiveOrdersMode || Order == null) return false;
                var platformId = Order.PlatformId2;
                var orderStatus = Order.ApiStatus;
                bool isReady = string.Equals(orderStatus, "READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase) || string.Equals(orderStatus, "SERVED", StringComparison.OrdinalIgnoreCase);
                return platformId != 9 && !isReady;
            }
        }
        public bool ShowHamburgerMenu
        {
            get
            {
                // In ViewOnly mode (HistoryPage), only show if platformId2 == 9
                if (IsViewOnlyMode)
                {
                    return ShowTransactions; // ShowTransactions returns true when platformId2 == 9
                }
                // In Kitchen and Tables modes, always show
                return true;
            }
        }
        //Hidden for table orders (platform 8) paid via Stripe/PayHere/Verifone or Hide Refund in ViewOnly mode
        public bool ShowRefundMenuItem => IsPaidForRefund && !IsViewOnlyMode;
        public bool ShowCancelMenuItem => CanCancel && !IsViewOnlyMode; // Hide cancel in ViewOnly mode
        public bool CanCancel
        {
            get
            {
                var status = (Order?.ApiStatus ?? string.Empty).ToUpper();
                return status == "QUEUE" || status == "ACCEPTED" || status == "PREPARING" || status == "READY" || status == "READY_FOR_PICKUP" || status == "SERVED" || status == "DELIVERED";
            }
        }
        public bool ShowKitchenUpdateOrderButton
        {
            get
            {
                if (!IsKitchenMode || Order == null) return false;
                // Hide for incoming Table Orders
                var isTableOrderPlatform = Order.PlatformId == 8
                    || string.Equals(Order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.Platform, "Table order", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Table order", StringComparison.OrdinalIgnoreCase);

                // Hide if payment status is PAID
                string paymentStatusText = (Order.PaymentStatus ?? string.Empty).Trim();
                bool isPaidByStatus = paymentStatusText.StartsWith("PAID", StringComparison.OrdinalIgnoreCase);
                bool isPaid = Order.IsPaid == true || isPaidByStatus;

                if (isTableOrderPlatform || isPaid) return false;
                return Order.OrderType == OrderType.DineIn;
            }
        }

        public bool ShowTablesUpdateOrderButton
        {
            get
            {
                if (!IsTablesMode || Order == null) return false;
                var isTableOrderPlatform = Order.PlatformId == 8
                    || string.Equals(Order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.Platform, "Table order", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Table order", StringComparison.OrdinalIgnoreCase);
                if (isTableOrderPlatform) return false;
                
                // Hide if payment status is PAID
                string paymentStatusText = (Order.PaymentStatus ?? string.Empty).Trim();
                bool isPaidByStatus = paymentStatusText.StartsWith("PAID", StringComparison.OrdinalIgnoreCase);
                bool isPaid = Order.IsPaid == true || isPaidByStatus;
                if (isPaid) return false;
                
                return true;
            }
        }

        public bool LiveOrdersUpdateOrderButton
        {
            get
            {
                if (!IsLiveOrdersMode || Order == null) return false;

                 // Hide for incoming Table Orders
                var isTableOrderPlatform = Order.PlatformId == 8 || Order.PlatformId2 == 8
                    || string.Equals(Order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.Platform, "Table order", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Table order", StringComparison.OrdinalIgnoreCase);

                // Hide if payment status is PAID
                string paymentStatusText = (Order.PaymentStatus ?? string.Empty).Trim();
                bool isPaidByStatus = paymentStatusText.StartsWith("PAID", StringComparison.OrdinalIgnoreCase);
                bool isPaid = Order.IsPaid == true || isPaidByStatus;

                if (isTableOrderPlatform || isPaid) return false;
                return Order.OrderType == OrderType.DineIn;
            }
        }

        public bool ShowDelayOrderButton
        {
            get
            {
                if (!IsKitchenMode || Order == null) return false;
                
                // Show delay order button only for accepted orders from Uber Eats
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                if (status != "ACCEPTED") return false;
                
                // Check if platform is Uber Eats (PlatformId == 1 or PlatformId2 == 1)
                var isUberEats = Order.PlatformId2 == 2 
                    || string.Equals(Order.PlatformName, "UBER_EATS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Uber Eats", StringComparison.OrdinalIgnoreCase);
                
                // Don't show delay button if order has already been delayed
                if (Order.OrderDelayed) return false;
                
                return isUberEats;
            }
        }

        public string PrimaryActionText
        {
            get
            {
                if (Order == null) return string.Empty;
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                return status switch
                {
                    "QUEUE" or "ACCEPTED" => "Move to Preparing",
                    "PREPARING" => "Move to Ready",
					"READY" => (Order.PlatformId == 8
						? "Complete Order"
						: (Order.OrderType == OrderType.DineIn
								? "Move to Served"
							: (Order.OrderType == OrderType.Delivery ? "Move to Dispatched" : "Move to Complete"))),
                    "READY_FOR_PICKUP" => (Order.IsTableOrderDineIn ? "Move to Served" : "Complete Order"),
                    "SERVED" => (Order.IsTableOrderDineIn ? "Complete order" : "Finish order"),
                    "DELIVERED" => "Complete order",
                    _ => ""
                };
            }
        }

        public KitchenOrderDetailsDialogViewModel() 
        {
            _apiService = new ApiService();
            _cartService = CartService.Instance;
            _dialogMode = DialogMode.Kitchen; // Default to kitchen mode
            InitializeCommands();
        }
        
        public KitchenOrderDetailsDialogViewModel(OrderModel order)
        {
            _apiService = new ApiService();
            _cartService = CartService.Instance;
            Order = order;
            _dialogMode = DialogMode.Kitchen; // Default to kitchen mode
            InitializeCommands();
        }

        public KitchenOrderDetailsDialogViewModel(int orderId)
        {
            _apiService = new ApiService();
            _cartService = CartService.Instance;
            _dialogMode = DialogMode.Kitchen; // Default to kitchen mode
            InitializeCommands();
            _ = LoadOrderDetailsAsync(orderId);
        }
        
        public KitchenOrderDetailsDialogViewModel(int orderId, DialogMode mode)
        {
            _apiService = new ApiService();
            _cartService = CartService.Instance;
            _dialogMode = mode;
            InitializeCommands();
            _ = LoadOrderDetailsAsync(orderId);
        }

        private void InitializeCommands()
        {
            PrintCommand = new RelayCommand(PrintOrder);
            PrimaryActionCommand = new RelayCommand(ExecutePrimaryAction);
            UpdateOrderCommand = new RelayCommand(UpdateOrder);
            FinishOrderCommand = new RelayCommand(() => FinishOrder());
            CancelOrderCommand = new RelayCommand(CancelOrder);
            DelayOrderCommand = new RelayCommand(OpenDelayDialog);
            RefundOrderCommand = new RelayCommand(RefundOrder);
            OpenTransactionsCommand = new RelayCommand(OpenTransactions);
            CompleteOrderLiveOrdersCommand = new RelayCommand(PaymentOrderLiveOrders);
            PaymentOrderLiveOrdersCommand = new RelayCommand(PaymentOrderLiveOrders);
            ReadyOrderLiveOrdersCommand = new RelayCommand(ReadyOrderLiveOrders);
        }

        private async Task LoadOrderDetailsAsync(int orderId)
        {
            try
            {
                IsLoading = true;
                var orderDetails = await _apiService.GetOrderByIdAsync(orderId);
                Order = orderDetails;
                
                // Populate shop fees for display
                ShopFeeRows.Clear();
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (Order?.OrderShopFees != null && Order.OrderShopFees.Count > 0)
                {
                    foreach (var fee in Order.OrderShopFees)
                    {
                        if (fee.Amount > 0)
                        {
                            string bracket;
                            var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                            decimal feeDisplayValue = fee.FeeValue;
                            if (feeDisplayValue <= 0m && shopDetails?.ShopFees != null && fee.ShopFeeId > 0)
                            {
                                var configured = shopDetails.ShopFees.FirstOrDefault(sf => sf.Id == fee.ShopFeeId);
                                if (configured != null && configured.Fee > 0m)
                                {
                                    feeDisplayValue = configured.Fee;
                                }
                            }

                            if (type == "PERCENTAGE")
                            {
                                bracket = feeDisplayValue > 0m ? $"{feeDisplayValue:0.##}%" : "percentage";
                            }
                            else if (type == "VALUE")
                            {
                                bracket = "value";
                            }
                            else
                            {
                                // Default to value when type is missing/unknown
                                bracket = "value";
                            }
                            var label = string.IsNullOrEmpty(bracket) ? fee.Name : $"{fee.Name}({bracket})";
                            ShopFeeRows.Add(new ShopFeeDisplayModel
                            {
                                Label = label,
                                Amount = fee.Amount,
                                TaxAmount = fee.TaxAmount,
                                TaxLabel = fee.TaxAmount > 0
                                    ? $"{fee.TaxCode ?? "Tax"} ({fee.TaxRate:0.##}%)"
                                    : string.Empty
                            });
                        }
                    }
                }
                
                OnPropertyChanged(nameof(ShopFeeRows));
                OnPropertyChanged(nameof(HasShopFees));
                OnPropertyChanged(nameof(TotalShopFees));
                OnPropertyChanged(nameof(DeliveryCharges));
                OnPropertyChanged(nameof(HasDeliveryCharges));
                
                // Populate tax details from API order_taxes array
                OrderTaxesFromApi.Clear();
                if (Order?.TaxSummaryRows != null && Order.TaxSummaryRows.Count > 0)
                {
                    foreach (var tax in Order.TaxSummaryRows)
                    {
                        OrderTaxesFromApi.Add(tax);
                    }
                }
                OnPropertyChanged(nameof(OrderTaxesFromApi));
                OnPropertyChanged(nameof(HasOrderTaxesFromApi));
                
                UpdateOrderTaxSummary();
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

        private void UpdateOrderTaxSummary()
        {
            OrderTaxSummaryRows.Clear();
            var shop = GlobalDataService.Instance.ShopDetails;
            var taxMode = (shop?.TaxMode ?? "none").Trim();
            var hasTaxProfiles = (shop?.TaxProfiles?.Count ?? 0) > 0;
            var hasOrderItems = Order?.Items != null && Order.Items.Count > 0;
            var hasOrderFees = Order?.OrderShopFees != null && Order.OrderShopFees.Count > 0;
            if (string.Equals(taxMode, "none", StringComparison.OrdinalIgnoreCase) || !hasTaxProfiles || (!hasOrderItems && !hasOrderFees))
            {
                OnPropertyChanged(nameof(HasOrderTaxSummary));
                return;
            }

            if (!ShouldShowTaxSummary(Order))
            {
                OnPropertyChanged(nameof(HasOrderTaxSummary));
                return;
            }

            var summary = new Dictionary<string, TaxSummaryRow>(StringComparer.OrdinalIgnoreCase);
            var taxInclusive = shop?.TaxInclusive ?? true;
            void AccumulateDetail(string codeValue, decimal rate, decimal taxAmount, decimal taxableAmount, bool allowEstimate)
            {
                var normalizedTaxAmount = Math.Round(Math.Max(0m, taxAmount), 2, MidpointRounding.AwayFromZero);
                var normalizedTaxable = taxableAmount;
                if ((normalizedTaxable <= 0m) && allowEstimate && normalizedTaxAmount > 0m && rate > 0m)
                {
                    normalizedTaxable = EstimateTaxable(normalizedTaxAmount, rate, taxInclusive);
                }

                if (normalizedTaxAmount <= 0m && normalizedTaxable <= 0m && !allowEstimate)
                {
                    return;
                }

                var code = !string.IsNullOrWhiteSpace(codeValue)
                    ? codeValue
                    : (rate > 0m ? $"TAX_{rate:0.##}" : "ZERO");

                if (!summary.TryGetValue(code, out var row))
                {
                    row = new TaxSummaryRow
                    {
                        TaxCode = code,
                        Rate = rate
                    };
                    summary[code] = row;
                }

                row.TaxAmount = Math.Round(row.TaxAmount + normalizedTaxAmount, 2, MidpointRounding.AwayFromZero);
                row.TaxableAmount = Math.Round(row.TaxableAmount + Math.Max(0m, normalizedTaxable), 2, MidpointRounding.AwayFromZero);
                if (row.Rate <= 0m && rate > 0m)
                {
                    row.Rate = rate;
                }
            }

            if (hasOrderItems)
            {
                foreach (var item in Order.Items)
                {
                    if (item?.TaxDetails == null || item.TaxDetails.Count == 0) continue;

                    var grouped = item.TaxDetails
                        .Where(d => d != null)
                        .GroupBy(d => new
                        {
                            Code = !string.IsNullOrWhiteSpace(d.TaxCode) ? d.TaxCode : $"TAX_{d.Rate:0.##}",
                            Rate = d.Rate
                        });

                    foreach (var group in grouped)
                    {
                        // Separate component details (modifiers) from primary details (base product)
                        var componentDetails = group.Where(d => d.IsComponentDetail).ToList();
                        var primaryDetails = group.Where(d => !d.IsComponentDetail).ToList();
                        
                        decimal taxAmount = 0m;
                        decimal taxableAmount = 0m;
                        
                        // Primary details (base product) - already line totals, don't multiply
                        if (primaryDetails.Count > 0)
                        {
                            taxAmount += primaryDetails.Sum(d => d.Amount);
                            taxableAmount += primaryDetails.Sum(d => d.TaxableAmount);
                        }
                        
                        // Component details (modifiers) - per unit, multiply by quantity
                        if (componentDetails.Count > 0)
                        {
                            var componentTax = componentDetails.Sum(d => d.Amount);
                            var componentTaxable = componentDetails.Sum(d => d.TaxableAmount);
                            
                            // Multiply by item quantity for modifiers
                            taxAmount += componentTax * item.Quantity;
                            taxableAmount += componentTaxable * item.Quantity;
                        }
                        
                        var needsEstimate = taxableAmount <= 0m;
                        AccumulateDetail(group.Key.Code, group.Key.Rate, taxAmount, taxableAmount, needsEstimate);
                    }
                }
            }

            if (Order.OrderShopFees != null)
            {
                foreach (var fee in Order.OrderShopFees)
                {
                    if (fee == null) continue;
                    var feeTaxable = Math.Round(Math.Max(0m, fee.Amount), 2, MidpointRounding.AwayFromZero);
                    var allowEstimate = feeTaxable <= 0m;
                    AccumulateDetail(fee.TaxCode, fee.TaxRate, fee.TaxAmount, feeTaxable, allowEstimate);
                }
            }

            if (Order.DeliveryTaxDetail != null)
            {
                var delivery = Order.DeliveryTaxDetail;
                var deliveryTaxable = delivery.TaxableAmount > 0 ? delivery.TaxableAmount : Order.DeliveryCharge;
                var needsEstimate = deliveryTaxable <= 0m;
                AccumulateDetail(delivery.TaxCode, delivery.Rate, delivery.Amount, deliveryTaxable, needsEstimate);
            }

            // Distribute Order Discount and Coupon across summary rows
            var totalDeduction = (Order.DiscountAmount) + (Order.VoucherDiscount);
            if (totalDeduction > 0)
            {
                var totalTaxable = summary.Values.Sum(r => r.TaxableAmount);
                if (totalTaxable > 0)
                {
                    decimal remainingDeduction = totalDeduction;
                    // Create a list to iterate safely
                    var rows = summary.Values.OrderByDescending(r => r.TaxableAmount).ToList();
                    
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        if (row.TaxableAmount <= 0) continue;

                        decimal share = 0m;
                        // For the last row (or effectively last with value), apply remaining to avoid rounding gaps
                        // Note: proportional distribution logic
                        if (remainingDeduction <= 0) break;
                        
                        // Calculate share based on proportion of this row to total taxable
                        // This assumes the discount applies to all taxable items equally
                        share = Math.Round(totalDeduction * (row.TaxableAmount / totalTaxable), 2, MidpointRounding.AwayFromZero);
                        
                        // Adjust for last item rounding
                        if (i == rows.Count - 1)
                        {
                            share = remainingDeduction;
                        }

                        // Ensure we don't deduct more than available on the row (unless negative tax is allowed, but let's cap at 0 for now)
                        // Also ensure we don't deduct more than remaining
                        var actualDeduction = Math.Min(row.TaxableAmount, share);
                        actualDeduction = Math.Min(actualDeduction, remainingDeduction);
                        
                        row.TaxableAmount = Math.Max(0m, row.TaxableAmount - actualDeduction);
                        remainingDeduction -= actualDeduction;
                    }
                    
                    // If any deduction remains (due to capping), force apply to the largest row
                    if (remainingDeduction > 0)
                    {
                         var largestRow = rows.FirstOrDefault();
                         if (largestRow != null)
                         {
                             largestRow.TaxableAmount = Math.Max(0m, largestRow.TaxableAmount - remainingDeduction);
                         }
                    }
                }
            }

            // Recalculate tax amounts based on updated taxable after discount/coupon distribution
            foreach (var row in summary.Values)
            {
                if (row.Rate <= 0m || row.TaxableAmount <= 0m)
                {
                    row.TaxAmount = 0m;
                    continue;
                }

                if (taxInclusive)
                {
                    // Tax inclusive: taxable includes tax; extract tax portion
                    // Multiply first to maintain precision, then divide
                    row.TaxAmount = Math.Round((row.TaxableAmount * row.Rate) / (100m + row.Rate), 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    // Tax exclusive: tax is applied on taxable base
                    // Multiply first to maintain precision, then divide
                    row.TaxAmount = Math.Round((row.TaxableAmount * row.Rate) / 100m, 2, MidpointRounding.AwayFromZero);
                }
            }

            foreach (var row in summary.Values.OrderByDescending(r => r.Rate).ThenBy(r => r.TaxCode))
            {
                OrderTaxSummaryRows.Add(row);
            }

            OnPropertyChanged(nameof(HasOrderTaxSummary));
        }

        private static decimal EstimateTaxable(decimal taxAmount, decimal rate, bool taxInclusive)
        {
            if (rate <= 0m) return 0m;
            if (taxInclusive)
            {
                return Math.Round(taxAmount * (100m + rate) / rate, 2, MidpointRounding.AwayFromZero);
            }
            return Math.Round(taxAmount * (100m / rate), 2, MidpointRounding.AwayFromZero);
        }

        private static bool ShouldShowTaxSummary(OrderModel order)
        {
            if (order == null) return false;

            // External platform mappings: 1=Uber, 2=Deliveroo, 6=Webshop, 8=Table Order
            var externalPlatformIds = new HashSet<int> { 1, 2, 6, 8 };
            if (externalPlatformIds.Contains(order.PlatformId2))
            {
                return false;
            }

            if (string.Equals(order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private async void PrintOrder()
        {
            try
            {
                if (Order == null)
                {
                    MessageBox.Show("No order available to print.", "Print", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Determine payment method text
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

                // Check printer settings: if both main and kitchen are enabled on at least one printer, show selection dialog; otherwise print the single enabled type.
                var printers = PrintersService.Instance.Printers;
                bool anyMain = false, anyKitchen = false;
                foreach (var p in printers)
                {
                    if (!p.IsActive) continue;
                    var s = PrinterSettingsService.Instance.GetPrinterSettings(p.DeviceName);
                    if (s == null) continue;
                    anyMain |= s.MainReceipt;
                    anyKitchen |= s.KitchenReceipt;
                }

                if (anyMain && anyKitchen)
                {
                    // Both types enabled: show PrintSelectionDialog so user can choose Main, Kitchen, or Both
                    var vm = new PrintSelectionDialogViewModel();
                    var win = new POS_UI.View.PrintSelectionWindow { DataContext = vm };
                    if (System.Windows.Application.Current?.MainWindow != null)
                    {
                        win.Owner = System.Windows.Application.Current.MainWindow;
                    }
                    // Hook close callback so VM can close the window
                    vm.RequestClose = () => { try { win.DialogResult = true; } catch { } win.Close(); };
                    win.ShowDialog();
                    var choice = vm.SelectedChoice; // "MAIN", "KITCHEN", "BOTH" or null
                    // Manual print: use onlyWhenMainReceiptOnOrder: false so main receipt prints even if "Print main receipt on order" is unchecked
                    if (choice == "MAIN")
                    {
                        _ = ReceiptPrintingService.Instance.PrintIncomingMainReceiptAsync(Order, paymentMethod, onlyWhenMainReceiptOnOrder: false);
                    }
                    else if (choice == "KITCHEN")
                    {
                        _ = ReceiptPrintingService.Instance.PrintIncomingKitchenReceiptAsync(Order);
                    }
                    else if (choice == "BOTH")
                    {
                        _ = ReceiptPrintingService.Instance.PrintIncomingMainReceiptAsync(Order, paymentMethod, onlyWhenMainReceiptOnOrder: false);
                        _ = ReceiptPrintingService.Instance.PrintIncomingKitchenReceiptAsync(Order);
                    }
                }
                else if (anyMain)
                {
                    // Only main receipt configured: print main receipt (no dialog). Requirement: if user has main receipt marked, Print must print main receipt.
                    _ = ReceiptPrintingService.Instance.PrintIncomingMainReceiptAsync(Order, paymentMethod, onlyWhenMainReceiptOnOrder: false);
                }
                else if (anyKitchen)
                {
                    // Only kitchen receipt configured: print kitchen receipt (no dialog)
                    _ = ReceiptPrintingService.Instance.PrintIncomingKitchenReceiptAsync(Order);
                }
                else
                {
                    MessageBox.Show("No printer has Main Receipt or Kitchen Receipt enabled. Please check printer settings.", "Print", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error printing order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecutePrimaryAction()
        {
            if (Order == null) return;
            
            try
            {
                IsLoading = true;
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                // Local storage key used for dine-in files
                var displayKey = Order?.OrderNumber ?? Order?.DisplayOrderId;
                switch (status)
                {
                    case "QUEUE":
                    case "ACCEPTED":
                        // If platform is Uber(1), Deliveroo(2) or Webshop(6), notify delivery platform first
                        if (Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6 || Order.PlatformId2 == 8 || Order.PlatformName == "TABLE_ORDER")
                        {
                            var remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                                ? Order.RemoteOrderId
                                : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                            var result = await _apiService.NotifyPreparingToDeliveryPlatformAsync(remoteOrderId);
                            if (!result.IsSuccess)
                            {
                                MessageBox.Show($"Failed to notify delivery platform: {result.ErrorMessage}");
                                return;
                            }
                        }
                        else
                        {
                            await _apiService.UpdateOrderStatusAsync(Order.ApiId, "PREPARING");
                        }
                        Order.ApiStatus = "PREPARING";
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(displayKey))
                            {
                                // Ensure local file exists even if not created yet; seed from API order
                                await POS_UI.Services.DineInOrderService.Instance.SeedOrUpdateFromOrderModelAsync(Order, POS_UI.Models.DineInOrderItemStatus.PREPARE);
                            }
                        }
                        catch {}
                        GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "PREPARING");
                        break;
                    case "PREPARING":
                        if (Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6 || Order.PlatformId2 == 8 || Order.PlatformName == "TABLE_ORDER")
                        {
                            var remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                                ? Order.RemoteOrderId
                                : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                            var result = await _apiService.NotifyReadyToPickupToDeliveryPlatformAsync(remoteOrderId);
                            if (!result.IsSuccess)
                            {
                                MessageBox.Show($"Failed to notify delivery platform: {result.ErrorMessage}");
                                return;
                            }
                        }
                        else
                        {
                            await _apiService.UpdateOrderStatusAsync(Order.ApiId, "READY");
                        }
                        Order.ApiStatus = "READY";
                        try { if (!string.IsNullOrWhiteSpace(displayKey)) await POS_UI.Services.DineInOrderService.Instance.UpdateAllItemsStatusAsync(displayKey, POS_UI.Models.DineInOrderItemStatus.READY); } catch {}
                        GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "READY");
                        break;
                    case "READY":
                    case "READY_FOR_PICKUP":
                        // Table order Dine-in in READY_FOR_PICKUP: Move to Served (notify platform then update to SERVED)
                        if (Order.IsTableOrderDineIn)
                        {
                            var remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                                ? Order.RemoteOrderId
                                : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                            var notifyResult = await _apiService.NotifyServeOrderToDeliveryPlatformAsync(remoteOrderId);
                            if (!notifyResult.IsSuccess)
                            {
                                MessageBox.Show($"Failed to notify served: {notifyResult.ErrorMessage}");
                                return;
                            }
                            try { if (!string.IsNullOrWhiteSpace(displayKey)) await POS_UI.Services.DineInOrderService.Instance.UpdateAllItemsStatusAsync(displayKey, POS_UI.Models.DineInOrderItemStatus.SERVED); } catch {}
                            GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "SERVED");
                            break;
                        }
                        // For other platform/table orders, route to shared completion logic
                        if (Order.PlatformId2 == 8 || Order.PlatformName == "TABLE_ORDER" || Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6)
                        {
                            // Close this dialog first to avoid nested hosts, then use shared flow
                            CloseDialog();
                            await Task.Delay(150);
                            await KitchenViewModel.MoveToCompletedStatic(Order);
                            return;
                        }
                        else if (Order.OrderType == OrderType.DineIn)
                        {
                            await _apiService.UpdateOrderStatusAsync(Order.ApiId, "SERVED");
                            Order.ApiStatus = "SERVED";
                            try { if (!string.IsNullOrWhiteSpace(displayKey)) await POS_UI.Services.DineInOrderService.Instance.UpdateAllItemsStatusAsync(displayKey, POS_UI.Models.DineInOrderItemStatus.SERVED); } catch {}
                            GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "SERVED");
                        }
                        else if (Order.OrderType == OrderType.Delivery)
                        {
                            await _apiService.UpdateOrderStatusAsync(Order.ApiId, "DELIVERED");
                            Order.ApiStatus = "DELIVERED";
                            try { if (!string.IsNullOrWhiteSpace(displayKey)) await POS_UI.Services.DineInOrderService.Instance.CompleteOrderAsync(displayKey); } catch {}
                            GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "DELIVERED");
                        }
                        /*else if (Order.OrderType == OrderType.TakeAway && Order.PaymentStatus != "PAID")
                        {
                            CloseDialog();
                            await Task.Delay(150);
                            MessageBox.Show("Opening checkout dialog");
                            MessageBox.Show($"Order ID: {Order.ApiId}");
                            MessageBox.Show($"Order type: {Order.OrderType}");
                            MessageBox.Show($"Order payment status: {Order.PaymentStatus}");
                            
                            var checkoutVm = new POS_UI.ViewModels.KitchenCheckoutViewModel(Order);
                            var checkoutDialog = new POS_UI.View.CheckoutDialog { DataContext = checkoutVm };
                            await MaterialDesignThemes.Wpf.DialogHost.Show(checkoutDialog, "RootDialog");
                        }*/
                        else
                        {
                            CloseDialog();
                            await Task.Delay(150);
                            // Route to shared completion logic to enforce checkout for unpaid
                            await KitchenViewModel.MoveToCompletedStatic(Order);
                        }
                        break;
                    /*case "READY_FOR_PICKUP":
                        if(Order.PlatformId == 8)
                        {
                            var tableId = Order.TableNumber;
                            //MessageBox.Show($"Updating table status to available for {tableId}");
                            if (tableId.HasValue && tableId.Value > 0)
                            {
                                await _apiService.UpdateTableStatusAsync(tableId.Value, "AVAILABLE", 0);
                            }
                            
                            var remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                                ? Order.RemoteOrderId
                                : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                            //MessageBox.Show($"Notifying delivery platform about completion for {remoteOrderId}");
                            var notifyResult = await _apiService.NotifyCompleteOrderToDeliveryPlatformAsync(remoteOrderId);
                            // Hide loader before showing result dialog
                        }
                        //await _apiService.UpdateOrderStatusAsync(Order.ApiId, "COMPLETED");
                        //Order.ApiStatus = "COMPLETED";
                        try { if (!string.IsNullOrWhiteSpace(displayKey)) await POS_UI.Services.DineInOrderService.Instance.CompleteOrderAsync(displayKey); } catch {}
                        GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "COMPLETED");
                        break;*/
                    case "SERVED":
                        CloseDialog();
                        // Table order Dine-in: Complete order (opens checkout when unpaid, same as orders page Served card)
                        if (Order.IsTableOrderDineIn)
                        {
                            await Task.Delay(150);
                            await KitchenViewModel.MoveToCompletedStatic(Order);
                            return;
                        }
                        // Other Dine-in: Finish order (load into cart / finish flow)
                        await KitchenViewModel.MoveToFinishedStatic(Order);
                        return;
                    case "DELIVERED":
                        try
                        {
                            // Close this dialog first to avoid nested RootDialog issue
                            CloseDialog();
                            await Task.Delay(200);

                            // Route to shared completion logic to enforce checkout for unpaid
                            await KitchenViewModel.MoveToCompletedStatic(Order);
                        }
                        catch {}
                        break;
                    default:
                        return;
                }
                OnPropertyChanged(nameof(Order));
                //OnPropertyChanged(nameof(PrimaryActionText));
                //OnPropertyChanged(nameof(TableButtonText));
                OnPropertyChanged(nameof(ShowKitchenUpdateOrderButton));
                OnPropertyChanged(nameof(PaymentStatus));
                OnPropertyChanged(nameof(LiveOrdersUpdateOrderButton));
                OnPropertyChanged(nameof(PaymentMethod));
                OnPropertyChanged(nameof(PaymentMode));
                OnPropertyChanged(nameof(ShippingMethodText));
                OnPropertyChanged(nameof(PlatformLogo));
                OnPropertyChanged(nameof(CanCancel));

                // Close the dialog after successful action
                CloseDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating order status: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void CancelOrder()
        {
            try
            {
                if (Order == null) return;
                _shouldReopenTableOrderListAfterClose = false;
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                //if (status != "QUEUE" && status != "ACCEPTED") return;
               
               // Determine if this order should show refund modal or confirmation prompt
                string paymentStatusText = (Order?.PaymentStatus ?? string.Empty).Trim();
                bool isPaidByStatus = paymentStatusText.StartsWith("PAID", StringComparison.OrdinalIgnoreCase);
                bool isPaid = Order?.IsPaid == true || isPaidByStatus;
                string method = (Order?.PaymentMethod ?? string.Empty).Trim().ToUpperInvariant();
                bool isCardMethod = method == "CARD" || method == "TERMINAL" || method == "MANUALCARD";
                // PlatformId2: 1=Uber, 2=Deliveroo, 6=Webshop; 8=TABLE_ORDER (treated as POS)
                bool isExternalPlatform = (Order?.PlatformId2 == 1) || (Order?.PlatformId2 == 2) || (Order?.PlatformId2 == 6) || (Order?.PlatformId2 == 8);
                bool hasRefundBalance = Order?.RefundBalance > 0;


                // For non-paid-card orders (cash, unpaid, or external), ask for a concise confirmation using app modal
                 // First, ask for cancellation reason
                string cancellationReason = null;
                if (!(isPaid && hasRefundBalance))
                {
               
                try
                {
                    IsDimmed = true;
                    var reasonVm = new POS_UI.ViewModels.CancelReasonDialogViewModel();
                    var reasonWin = new POS_UI.View.CancelReasonWindow { DataContext = reasonVm };
                    var owner = System.Windows.Application.Current?.Windows?.OfType<System.Windows.Window>()?.FirstOrDefault(w => w.IsActive)
                                ?? System.Windows.Application.Current?.MainWindow;
                    if (owner != null) reasonWin.Owner = owner;
                    var reasonOk = reasonWin.ShowDialog() == true;

                    // Remove dimming when dialog closes
                    IsDimmed = false;
                    if (!reasonOk || string.IsNullOrWhiteSpace(reasonWin.SelectedReason))
                    {
                        return; // User cancelled or didn't select a reason
                    }
                    cancellationReason = reasonWin.SelectedReason;
                }
                catch
                {
                    // Remove dimming on error
                    IsDimmed = false;
                    return; // If reason dialog fails, cancel the operation
                }
                
                    try
                    {
                        IsDimmed = true;
                        var vm = new POS_UI.ViewModels.ConfirmDialogViewModel
                        {
                            Title = "Confirm Cancellation",
                            Message = "Please confirm if you would like to cancel this order."
                        };
                        var win = new POS_UI.View.ConfirmWindow { DataContext = vm };
                        var owner = System.Windows.Application.Current?.Windows?.OfType<System.Windows.Window>()?.FirstOrDefault(w => w.IsActive)
                                    ?? System.Windows.Application.Current?.MainWindow;
                        if (owner != null) win.Owner = owner;
                        var ok = win.ShowDialog() == true;

                        // Remove dimming when dialog closes
                        IsDimmed = false;
                        if (!ok) return;
                    }
                    catch
                    {
                        // Remove dimming on error
                        IsDimmed = false;
                        var fallback = MessageBox.Show("Please confirm if you would like to cancel this order.", "Confirm Cancellation", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                        if (fallback != MessageBoxResult.Yes) return;
                    }
                }

                // If order is paid and not external platform, open RefundFlowDialog directly
                if (isPaid && hasRefundBalance)
                {

                    // Check user role - get from API since role is not in JWT token
                    string userRole = string.Empty;
                    try
                    {
                        var currentUserApi = await _apiService.GetCurrentUserAsync();
                        userRole = currentUserApi?.Role ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to get current user role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    //MessageBox.Show($"RefundOrder - User role: {userRole}");
                    
                    //dialog host id
                    string dialogHostID = _dialogMode == DialogMode.Tables ? "RootDialogHost" : "RootDialog";
                    bool isCashier = !string.IsNullOrEmpty(userRole) && 
                        userRole.Replace(" ", "", StringComparison.OrdinalIgnoreCase)
                            .Equals("Cashier", StringComparison.OrdinalIgnoreCase);
                    // If cashier, show admin credentials dialog first
                    if (isCashier)
                    {
                        // Close any open dialog host before opening admin credentials dialog
                        try
                        {
                            if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostID))
                            {
                                MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostID, true);
                                //await Task.Delay(120);
                            }
                        }
                        catch { }

                        var adminCredDialog = new POS_UI.View.AdminCredentialsDialog();
                        var adminCredResult = await MaterialDesignThemes.Wpf.DialogHost.Show(adminCredDialog, dialogHostID);

                        // If admin credentials not verified (canceled or false), return
                        if (adminCredResult == null || !(adminCredResult is bool result && result))
                        {
                            return;
                        }

                        // Wait a moment after admin credentials dialog closes
                        await Task.Delay(100);
                    }
                    // Determine which dialog host identifier to use based on dialog mode
                    string dialogHostId2 = _dialogMode == DialogMode.Tables ? "RootDialogHost" : "RootDialog";

                    await OpenRefundFlowAfterCloseAsync(dialogHostId2, isCancelOrderFlow: true);
                    return; // Exit early - cancellation and refund will be handled in the dialog
                }
                
                IsLoading = true;

                bool isDeliveryPlatform = Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6;
                bool isTableOrderPlatform = Order.PlatformId == 8
                    || Order.PlatformId2 == 8
                    || string.Equals(Order.PlatformName, "TABLE_ORDER", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.Platform, "Table order", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Table order", StringComparison.OrdinalIgnoreCase);

                string remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                    ? Order.RemoteOrderId
                    : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));

              /* if (isTableOrderPlatform)
                {
                    try
                    {
                        int[] tableIds = null;
                        if (Order.OrderSessionId.HasValue && Order.OrderSessionId.Value > 0)
                        {
                            tableIds = await _apiService.GetTableIdsFromSessionAsync(Order.OrderSessionId.Value);
                        }
                        if (tableIds == null || tableIds.Length == 0)
                        {
                            if (Order.TableNumber.HasValue && Order.TableNumber.Value > 0)
                                tableIds = new[] { Order.TableNumber.Value };
                        }
                        if (tableIds != null && tableIds.Length > 0)
                            {
                                foreach (var id in tableIds)
                                    await _apiService.UpdateTableStatusAsync(id, "AVAILABLE", 0);
                            }
                    }
                    catch (Exception tableEx)
                    {
                        MessageBox.Show($"Failed to update table status: {tableEx.Message}", "Cancel Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                } */

                if (Order.PlatformId2 == 9)
                {
                    await _apiService.UpdateOrderStatusAsync(Order.ApiId, "CANCELLED", cancellationReason);
                }
                else
                {
                    bool notifiedRemote = false;
                    bool shouldNotifyRemote = (isDeliveryPlatform || isTableOrderPlatform) && !string.IsNullOrWhiteSpace(remoteOrderId);

                    if (shouldNotifyRemote)
                    {
                        var notifyResult = await _apiService.NotifyCancelOrderToDeliveryPlatformAsync(remoteOrderId, cancellationReason);
                        if (!notifyResult.IsSuccess)
                        {
                            MessageBox.Show($"Failed to notify delivery platform: {notifyResult.ErrorMessage}", "Cancel Order", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        notifiedRemote = true;
                    }

                   /* if (!isDeliveryPlatform || !isTableOrderPlatform || !notifiedRemote)
                    {
                        await _apiService.UpdateOrderStatusAsync(Order.ApiId, "CANCELLED", cancellationReason);
                    }*/
                }

                Order.ApiStatus = "CANCELLED";
                GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "CANCELLED");


                OnPropertyChanged(nameof(Order));
                OnPropertyChanged(nameof(PrimaryActionText));
                OnPropertyChanged(nameof(CanCancel));

                // Determine which dialog host identifier to use based on dialog mode
                string dialogHostId = _dialogMode == DialogMode.Tables ? "RootDialogHost" : "RootDialog";

                // Close the dialog using async pattern with delay to ensure it closes properly
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostId, true);
                        }
                    }
                    catch
                    {
                        // If the specific dialog host doesn't exist, try the other one as fallback
                        try
                        {
                            string fallbackId = _dialogMode == DialogMode.Tables ? "RootDialog" : "RootDialogHost";
                            if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(fallbackId))
                            {
                                MaterialDesignThemes.Wpf.DialogHost.Close(fallbackId, true);
                            }
                        }
                        catch
                        {
                            // Silently ignore if neither dialog host exists
                        }
                    }
                });
                
                // Wait for dialog to close before refreshing
                await Task.Delay(100);
                
                // Refresh tables page if active to immediately update the UI
                RefreshTablesPageIfActive();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to cancel order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void RefundOrder()
        {
            try
            {
                if (Order == null) return;
                _shouldReopenTableOrderListAfterClose = false;

                // Check user role - get from API since role is not in JWT token
                string userRole = string.Empty;
                try
                {
                    var currentUserApi = await _apiService.GetCurrentUserAsync();
                    userRole = currentUserApi?.Role ?? string.Empty;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to get current user role: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                //MessageBox.Show($"RefundOrder - User role: {userRole}");
                
                //dialog host id
                string dialogHostId = _dialogMode == DialogMode.Tables ? "RootDialogHost" : "RootDialog";
                bool isCashier = !string.IsNullOrEmpty(userRole) && 
                    userRole.Replace(" ", "", StringComparison.OrdinalIgnoreCase)
                        .Equals("Cashier", StringComparison.OrdinalIgnoreCase);
                // If cashier, show admin credentials dialog first
                if (isCashier)
                {
                    // Close any open dialog host before opening admin credentials dialog
                    try
                    {
                        if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId))
                        {
                            MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostId, true);
                            //await Task.Delay(120);
                        }
                    }
                    catch { }

                    var adminCredDialog = new POS_UI.View.AdminCredentialsDialog();
                    var adminCredResult = await MaterialDesignThemes.Wpf.DialogHost.Show(adminCredDialog, dialogHostId);

                    // If admin credentials not verified (canceled or false), return
                    if (adminCredResult == null || !(adminCredResult is bool result && result))
                    {
                        return;
                    }

                    // Wait a moment after admin credentials dialog closes
                    await Task.Delay(100);
                }

                await OpenRefundFlowAfterCloseAsync(dialogHostId, isCancelOrderFlow: false);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open refund dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                var errorVm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", $"Failed to open refund dialog: {ex.Message}");
                var errorDlg = new POS_UI.View.StatusDialog { DataContext = errorVm };
                await MaterialDesignThemes.Wpf.DialogHost.Show(errorDlg, "RootDialog");
            }
        }

        private async Task OpenRefundFlowDialogAsync(string dialogHostId, bool isCancelOrderFlow = false)
        {
            var refundVm = new POS_UI.ViewModels.RefundFlowDialogViewModel(Order, isCancelOrderFlow: isCancelOrderFlow, _dialogMode);
            var refundDialog = new POS_UI.View.RefundFlowDialog { DataContext = refundVm };

            refundVm.OnRefundComplete = async (result) =>
            {
                MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostId);

                if (result != null && result.IsConfirmed)
                {
                    if (Order != null)
                    {
                        Order.RefundBalance = Math.Max(0, Order.RefundBalance - result.Amount);
                        OnPropertyChanged(nameof(PaymentStatus));
                        OnPropertyChanged(nameof(IsRefundedStatus));
                    }

                    await Task.Delay(200);
                    if (isCancelOrderFlow)
                    {
                        RefreshTablesPageIfActive();
                        return;
                    }
                    var currencySymbol = GlobalDataService.Instance.ShopDetails?.Currency ?? "£";
                    var successVm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess(
                        "Refund Processed",
                        $"Refund of {currencySymbol}{result.Amount:F2} has been processed successfully.\n\nOrder: {Order?.OrderNumber ?? Order?.DisplayOrderId ?? "N/A"}\nMode: {result.Mode}\nReason: {result.Reason}"
                    );
                    var successDlg = new POS_UI.View.StatusDialog { DataContext = successVm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(successDlg, dialogHostId);
                }
            };

            await MaterialDesignThemes.Wpf.DialogHost.Show(refundDialog, dialogHostId);
        }

        private async Task OpenRefundFlowAfterCloseAsync(string dialogHostId, bool isCancelOrderFlow)
        {
            // In tables flow, opening a new dialog immediately after Close from inside the same handler
            // can race and trigger "DialogHost is already open". Defer to dispatcher idle.
            _ = Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId))
                        MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostId, true);
                }
                catch { }
            }, DispatcherPriority.Send);

            await Task.Delay(250);
            int waitMs = 0;
            while (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId) && waitMs < 3000)
            {
                await Task.Delay(50);
                waitMs += 50;
            }

            // Cancel-order flow: open RefundFlowDialog for this order only; skip session order picker.
            if (!isCancelOrderFlow && Order.OrderSessionId.HasValue && Order.OrderSessionId.Value > 0)
            {
                var sessionOrdersResponse = await _apiService.GetSessionOrdersAsync(Order.OrderSessionId.Value);
                if (sessionOrdersResponse?.Data?.OrderDetails != null && sessionOrdersResponse.Data.OrderDetails.Count > 0)
                {
                    var sessionRefundVm = new SessionOrdersRefundDialogViewModel(
                        Order.OrderSessionId.Value,
                        Order,
                        dialogHostId,
                        _dialogMode);
                    sessionRefundVm.OnRefundRequested = async () =>
                    {
                        await OpenRefundFlowDialogAsync(dialogHostId, isCancelOrderFlow: isCancelOrderFlow);
                    };
                    var sessionRefundDialog = new SessionOrdersRefundDialog { DataContext = sessionRefundVm };
                    await MaterialDesignThemes.Wpf.DialogHost.Show(sessionRefundDialog, dialogHostId);
                    return;
                }
            }

            await OpenRefundFlowDialogAsync(dialogHostId, isCancelOrderFlow: isCancelOrderFlow);
        }
        
        private async void OpenDelayDialog()
        {
            try
            {
                if (Order == null) return;
                var status = (Order.ApiStatus ?? string.Empty).ToUpper();
                if (status != "ACCEPTED") return;

                var isUberEats = Order.PlatformId2 == 2 
                    || string.Equals(Order.PlatformName, "UBER_EATS", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Order.DeliveryPlatfornName, "Uber Eats", StringComparison.OrdinalIgnoreCase);
                if (!isUberEats) return;

                // Close any open dialog host before opening our selection dialog
                try
                {
                    if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialog"))
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", true);
                        await Task.Delay(120);
                    }
                    else if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialogHost"))
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close("RootDialogHost", true);
                        await Task.Delay(120);
                    }
                }
                catch { }

                var vm = new POS_UI.ViewModels.DelayOrderDialogViewModel();
                vm.RemoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                        ? Order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                
                // Set up callback to mark order as delayed when successful
                vm.OnDelaySuccess = () =>
                {
                    Order.OrderDelayed = true;
                    OnPropertyChanged(nameof(ShowDelayOrderButton));
                };
                
                var dialog = new POS_UI.View.DelayOrderDialog { DataContext = vm };
                var result = await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "RootDialog");

                // result is not used now; ConfirmDelayCommand closes dialog
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open delay dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void UpdateOrder()
        {
            try
            {
                if (Order == null)
                {
                    MessageBox.Show("No order available to update.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show loading indicator
                IsLoading = true;

                // Get the full order details from API to ensure we have all the latest data
                var fullOrderDetails = await _apiService.GetOrderByIdAsync(Order.ApiId);
                
                if (fullOrderDetails == null)
                {
                    MessageBox.Show("Failed to retrieve order details from server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Close the dialog
                CloseDialog();

                // ===== RECOMMENDED APPROACH: Global Data Service =====
                // Store order details in GlobalDataService for easy access
                // This includes: Customer Name, Table Number, Display Order ID
                GlobalDataService.Instance.CurrentOrderForEdit = fullOrderDetails;
                
                // Navigate to CashierHomePage
                // Ensure we are NOT in finish flow for normal update path
                POS_UI.Services.GlobalDataService.Instance.IsFinishFlow = false;
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.MainFrame.Navigate(new CashierHomePage());
                }
                else
                {
                    // Fallback navigation
                    var currentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                    if (currentWindow != null)
                    {
                        var frame = currentWindow.FindName("MainFrame") as System.Windows.Controls.Frame;
                        if (frame != null)
                        {
                            frame.Navigate(new CashierHomePage());
                        }
                    }
                }

                //MessageBox.Show($"Order {fullOrderDetails.OrderNumber} has been loaded for editing.", "Order Updated", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OpenTransactions()
        {
            try
            {
                if (Order == null) return;

                // Determine which dialog host identifier to use based on dialog mode
                string dialogHostId = _dialogMode == DialogMode.Tables ? "RootDialogHost" : "RootDialog";

                // Close any open dialog host before opening transactions dialog
                try
                {
                    if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId))
                    {
                        MaterialDesignThemes.Wpf.DialogHost.Close(dialogHostId, true);
                        // Wait until dialog is actually closed
                        while (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(dialogHostId))
                        {
                            await Task.Delay(50);
                        }
                        await Task.Delay(150); // Additional delay for animation to complete
                    }
                }
                catch { }

                // Create ViewModel with order transactions and show transactions dialog
                var transactionsViewModel = new TransactionDialogViewModel(Order, Order.Transactions, _dialogMode);
                var transactionsDialog = new POS_UI.View.TransactionDialog { DataContext = transactionsViewModel };
                await MaterialDesignThemes.Wpf.DialogHost.Show(transactionsDialog, dialogHostId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open transactions: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*private async void CompleteOrderLiveOrders()
        {
            if (Order == null || !ShowCompleteButtonLiveOrders) return;
            try
            {
                IsLoading = true;
                await _apiService.UpdateOrderStatusAsync(Order.ApiId, "COMPLETED");
                Order.ApiStatus = "COMPLETED";
                GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "COMPLETED");
                CloseDialog();
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", $"Failed to complete order: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            finally
            {
                IsLoading = false;
            }
        }*/

        private async void PaymentOrderLiveOrders()
        {
            // Allow when either "Complete" (paid) or "Pay & Complete" (unpaid) is shown
            if (Order == null || (!ShowCompleteButtonLiveOrders && !ShowPaymentButtonLiveOrders)) return;
            try
            {
                if (string.Equals(Order.ShippingMethod,"Dine-in",StringComparison.OrdinalIgnoreCase))
                {
                    CloseDialog();
                    await KitchenViewModel.MoveToFinishedStatic(Order);
                }
                else
                {
                    CloseDialog();
                    await KitchenViewModel.MoveToCompletedStatic(Order);
                }
               
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", $"Failed to open payment: {ex.Message}");
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
        }

        private async void ReadyOrderLiveOrders()
        {
            if (Order == null || !ShowreadyButtonLiveOrders) return;
            
            try
            {
                if (Order.PlatformId2 == 1 || Order.PlatformId2 == 2 || Order.PlatformId2 == 6 || Order.PlatformId2 == 8)
                {
                    var remoteOrderId = !string.IsNullOrWhiteSpace(Order.RemoteOrderId)
                        ? Order.RemoteOrderId
                        : (!string.IsNullOrWhiteSpace(Order.DisplayOrderId) ? Order.DisplayOrderId : (Order.OrderNumber ?? Order.ApiId.ToString()));
                    var result = await _apiService.NotifyReadyToPickupToDeliveryPlatformAsync(remoteOrderId);
                    if (!result.IsSuccess)
                    {
                        var vm = StatusDialogViewModel.CreateError("Failed to Notify Delivery Platform", result.ErrorMessage ?? "Unknown error");
                        var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                        MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                        return;
                    }
                    else
                    {
                        CloseDialog();
                        await LiveOrdersViewModel.Instance.LoadOrdersAsync();
                    }
                }
                
            }
            catch (Exception ex)
            {
                var vm = StatusDialogViewModel.CreateError("Error", ex.Message);
                var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
            }
            
        }

        private async void FinishOrder()
        {
            try
            {
                if (Order == null)
                {
                    MessageBox.Show("No order available to finish.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

				if (Order.PlatformId == 8 || Order.PlatformId2 == 8 || Order.PlatformName == "TABLE_ORDER")
                {
					// DialogHost "already open": Close() called from inside the dialog can be ignored/deferred by
					// MaterialDesign, so the host never closes. Never call Close from inside the dialog.
					// Schedule (1) close and (2) show next to run after this handler returns.
					const string hostId = "RootDialogHost";
					var orderToComplete = Order;
					// 1) Schedule close to run after handler returns (so we're not "inside" the dialog)
					_ = Application.Current.Dispatcher.InvokeAsync(() =>
					{
						try
						{
							if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(hostId))
								MaterialDesignThemes.Wpf.DialogHost.Close(hostId, true);
						}
						catch (Exception)
						{
							// MaterialDesign can throw when closing (e.g. "already open"); flow still works, suppress so no global error dialog
						}
					}, System.Windows.Threading.DispatcherPriority.Send);
					// 2) Schedule show-next to run later (after close has been processed)
					_ = Application.Current.Dispatcher.InvokeAsync(async () =>
					{
						await Task.Delay(600);
						int waitMs = 0;
						while (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(hostId) && waitMs < 3000)
						{
							await Task.Delay(50);
							waitMs += 50;
						}
						try
						{
							await KitchenViewModel.MoveToCompletedStatic(orderToComplete, dialogHostId: hostId);
						}
						catch (Exception ex)
						{
							System.Diagnostics.Debug.WriteLine($"MoveToCompletedStatic error: {ex.Message}");
							var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Error", ex.Message);
							if (!MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen(hostId))
								MaterialDesignThemes.Wpf.DialogHost.Show(new POS_UI.View.StatusDialog { DataContext = vm }, hostId);
						}
						RefreshTablesPageIfActive();
					}, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
					return;
                }
                else
                {
                    var IsPaid = string.Equals(Order.PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase);
                    if (IsPaid)
                    {
                        var result = await _apiService.UpdateOrderStatusAsync(Order.ApiId, "COMPLETED");
                        if (result)
                        {
                            Order.ApiStatus = "COMPLETED";
                            GlobalDataService.Instance.NotifyOrderStatusChanged(Order.ApiId, "COMPLETED");
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateSuccess("Order Completed", $"Order {Order.DisplayOrderId} Completed Successfully");
                            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                MaterialDesignThemes.Wpf.DialogHost.Close("RootDialogHost", true);
                            });
                            await Task.Delay(100);
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                            RefreshTablesPageIfActive();
                            return;
                        }
                        else
                        {
                            var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateError("Order Completion Failed", "Failed to complete order.");
                            var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                            MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialogHost");
                        }   
                    }
                    else
                    {

                
                        IsLoading = true;

                        // Set loading state to disable buttons during transition
                        var cashierViewModel = GetCashierViewModel();
                        if (cashierViewModel != null)
                        {
                            cashierViewModel.IsFinishOrderLoading = true;
                        }

                        // Check if this is a served dine-in order - if so, call KitchenViewModel's MoveToFinished logi
                        // For other order types or statuses, use the original logic
                        // Fetch full order details from API
                        var fullOrderDetails = await _apiService.GetOrderByIdAsync(Order.ApiId);
                        if (fullOrderDetails == null)
                        {
                            MessageBox.Show("Failed to retrieve order details from server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        // Close the dialog
                        CloseDialog();

                        // Mark finish flow and load into cart like kitchen finish
                        GlobalDataService.Instance.IsFinishFlow = true;
                        GlobalDataService.Instance.CurrentOrderForEdit = fullOrderDetails;

                        // Navigate to CashierHomePage
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.MainFrame.Navigate(new CashierHomePage());
                        }
                        else
                        {
                            var currentWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
                            if (currentWindow != null)
                            {
                                var frame = currentWindow.FindName("MainFrame") as System.Windows.Controls.Frame;
                                if (frame != null)
                                {
                                    frame.Navigate(new CashierHomePage());
                                }
                            }   
                        }  
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading order to finish: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                // Clear loading state after completion or error
                var cashierViewModel = GetCashierViewModel();
                if (cashierViewModel != null)
                {
                    cashierViewModel.IsFinishOrderLoading = false;
                }
            }
        }

        private string GetAddressFromIncomingOrder()
        {
            try
            {
                if (Order == null || string.IsNullOrWhiteSpace(Order.DisplayOrderId))
                    return string.Empty;

                // Get incoming order banners from GlobalDataService
                var incomingOrders = GlobalDataService.Instance.GetPersistentIncomingOrderBanners();
                
                // Find the matching order by DisplayOrderId
                var matchingOrder = incomingOrders.FirstOrDefault(io => 
                    !string.IsNullOrWhiteSpace(io.DisplayOrderId) && 
                    io.DisplayOrderId.Equals(Order.DisplayOrderId, StringComparison.OrdinalIgnoreCase));

                if (matchingOrder == null || string.IsNullOrWhiteSpace(matchingOrder.OrderJson))
                    return string.Empty;

                // Parse the JSON to extract address information
                using var doc = System.Text.Json.JsonDocument.Parse(matchingOrder.OrderJson);
                var order = doc.RootElement;

                // Try to get address from shipping_details first
                var addressParts = new List<string>();

                // Fallback: Extract address from delivergate_customer.address
                if (order.TryGetProperty("delivergate_customer", out var customerObj) && customerObj.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (customerObj.TryGetProperty("address", out var addressProp) && addressProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        var address = addressProp.GetString();
                        if (!string.IsNullOrWhiteSpace(address))
                            return address;
                    }
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting address from incoming order: {ex.Message}");
                return string.Empty;
            }
        }

        private void CloseDialog()
        {
            // Close the dialog by executing the close command
            try
            {
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialog"))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("RootDialog", true);
                    return;
                }
                if (MaterialDesignThemes.Wpf.DialogHost.IsDialogOpen("RootDialogHost"))
                {
                    MaterialDesignThemes.Wpf.DialogHost.Close("RootDialogHost", true);
                    return;
                }
            }
            catch (Exception ex)
            {
                // If DialogHost is not available, try alternative approach
                System.Diagnostics.Debug.WriteLine($"Error closing dialog: {ex.Message}");
            }
        }

        private async void RefreshTablesPageIfActive()
        {
            try
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow?.MainFrame?.Content is TablesPage page &&
                        page.DataContext is TablesViewModel tablesVm &&
                        tablesVm.RefreshTablesCommand?.CanExecute(null) == true)
                    {
                        tablesVm.RefreshTablesCommand.Execute(null);
                    }
                });
            }
            catch { }
        }

        private static CashierHomeViewModel GetCashierViewModel()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow?.DataContext is CashierHomeViewModel viewModel)
                {
                    return viewModel;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public class ShopFeeDisplayModel
        {
            public string Label { get; set; }
            public decimal Amount { get; set; }
            public decimal TaxAmount { get; set; }
            public string TaxLabel { get; set; }
            public bool HasTax => TaxAmount > 0m && !string.IsNullOrWhiteSpace(TaxLabel);
        }
    }
} 