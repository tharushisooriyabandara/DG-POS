using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Collections.Generic;
using POS_UI.Models;
using System.Threading.Tasks;
using System.Windows;
using System;


namespace POS_UI.ViewModels
{
    public class OrderDetailsDialogViewModel : INotifyPropertyChanged
    {
        public string OrderNumber { get; set; }
        
        private string _orderTypeTime;
        public string OrderTypeTime 
        { 
            get => _orderTypeTime;
            set
            {
                _orderTypeTime = value;
                OnPropertyChanged(nameof(OrderTypeTime));
                OnPropertyChanged(nameof(OrderTypeName));
                OnPropertyChanged(nameof(OrderTypeTimeFormatted));
            }
        }
        
        public string OrderTypeName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OrderTypeTime)) return string.Empty;
                
                // Extract order type part from OrderTypeTime (format: "Collection - 12.14 pm")
                var parts = OrderTypeTime.Split(new[] { " - " }, StringSplitOptions.None);
                if (parts.Length >= 1)
                {
                    return parts[0].Trim();
                }
                return OrderTypeTime;
            }
        }
        
        public string OrderTypeTimeFormatted
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OrderTypeTime)) return string.Empty;
                
                // Extract time part from OrderTypeTime (format: "Collection - 12.14 pm")
                var parts = OrderTypeTime.Split(new[] { " - " }, StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    var timePart = parts[1].Trim();
                    // Convert to uppercase PM/AM
                    timePart = timePart.Replace(" pm", " PM").Replace(" am", " AM");
                    return timePart;
                }
                return OrderTypeTime;
            }
        }
        public string Contact { get; set; }
        public string ContactAccessCode { get; set; }
        //public string PlatformName { get; set; }
        private string _platform;
        public string Platform
        {
            get => _platform;
            set
            {
                _platform = value;
                OnPropertyChanged(nameof(Platform));
                OnPropertyChanged(nameof(IsTableOrder));
                OnPropertyChanged(nameof(ShowContactAccessCode));
            }
        }
        public string PlatformLogoUrl { get; set; }
        public int PlatformId { get; set; }
        private string _deliveryType;
        public string DeliveryType
        {
            get => _deliveryType;
            set
            {
                _deliveryType = value;
                OnPropertyChanged(nameof(DeliveryType));
                OnPropertyChanged(nameof(DeliveryTypeDisplay));
            }
        }
        public string CustomerName { get; set; }
        // Numeric id sent by backend for table order method (e.g., 7 = waiter)
        private int _tableOrderMethodId;
        public int TableOrderMethodId
        {
            get => _tableOrderMethodId;
            set
            {
                _tableOrderMethodId = value;
                OnPropertyChanged(nameof(TableOrderMethodId));
            }
        }
        // Table Order specific metadata
        private string _tableOrderMethod;
        public string TableOrderMethod
        {
            get => _tableOrderMethod;
            set
            {
                _tableOrderMethod = value;
                OnPropertyChanged(nameof(TableOrderMethod));
                OnPropertyChanged(nameof(HasTableOrderMethod));
                OnPropertyChanged(nameof(ShowTableOrderMethod));
                OnPropertyChanged(nameof(DeliveryTypeDisplay));
            }
        }
        public bool HasTableOrderMethod => !string.IsNullOrWhiteSpace(TableOrderMethod);
        public bool ShowTableOrderMethod => IsTableOrder && HasTableOrderMethod;

        // Displayed in the yellow badge: for Table orders show method, else delivery type
        public string DeliveryTypeDisplay => ShowTableOrderMethod ? TableOrderMethod : DeliveryType;
        private string _paymentStatus;
        public string PaymentStatus
        {
            get => _paymentStatus;
            set
            {
                _paymentStatus = value;
                OnPropertyChanged(nameof(PaymentStatus));
                OnPropertyChanged(nameof(HasPaymentStatus));
            }
        }
        public bool HasPaymentStatus => !string.IsNullOrWhiteSpace(PaymentStatus);
        public ObservableCollection<OrderItemViewModel> Items { get; set; } = new ObservableCollection<OrderItemViewModel>();
        public string OrderNotes { get; set; }
        public decimal Subtotal { get; set; }
        public decimal OrderDiscount { get; set; }
        public string DiscountModeApplied { get; set; } = "percentage"; // "percentage" or "value"
        public decimal DiscountPercentage { get; set; }
        public string DiscountDescription 
        {
            get
            {
                // Only show discount description for POS orders (platform_id != 1, 2, 6)
                // Platform 1 = Uber, 2 = Deliveroo, 6 = Webshop
                if (PlatformId == 1 || PlatformId == 2 || PlatformId == 6)
                {
                    return "Discount"; // Keep original for delivery platforms
                }
                
                // For POS orders, show discount mode
                if (DiscountModeApplied == "percentage" && DiscountPercentage > 0)
                {
                    return $"Discount ({DiscountPercentage}%)";
                }
                else if (DiscountModeApplied == "value" && OrderDiscount > 0)
                {
                    return "Discount (value)";
                }
                else
                {
                    return "Discount";
                }
            }
        }
        public bool HasDiscount => OrderDiscount > 0 || DiscountPercentage > 0;
        public decimal Total { get; set; }
        
        // Address details (shown when delivery)
        private string _addressLine1;
        public string AddressLine1
        {
            get => _addressLine1;
            set
            {
                _addressLine1 = value;
                OnPropertyChanged(nameof(AddressLine1));
                OnPropertyChanged(nameof(HasAddress));
                OnPropertyChanged(nameof(AddressDisplay));
            }
        }

        private string _addressLine2;
        public string AddressLine2
        {
            get => _addressLine2;
            set
            {
                _addressLine2 = value;
                OnPropertyChanged(nameof(AddressLine2));
                OnPropertyChanged(nameof(HasAddress));
                OnPropertyChanged(nameof(AddressDisplay));
            }
        }

        public bool HasAddress => !string.IsNullOrWhiteSpace(AddressLine1) || !string.IsNullOrWhiteSpace(AddressLine2);
        public string AddressDisplay
        {
            get
            {
                if (!HasAddress) return string.Empty;
                if (!string.IsNullOrWhiteSpace(AddressLine1) && !string.IsNullOrWhiteSpace(AddressLine2))
                    return $"{AddressLine1}, {AddressLine2}";
                return AddressLine1 ?? AddressLine2 ?? string.Empty;
            }
        }
        
        // New properties for fee components
        private decimal _bogoDiscount;
        public decimal BogoDiscount 
        { 
            get => _bogoDiscount; 
            set 
            { 
                _bogoDiscount = value; 
                OnPropertyChanged(nameof(BogoDiscount));
                OnPropertyChanged(nameof(HasBogoDiscount));
            } 
        }
        
        private decimal _shopFee;
        public decimal ShopFee 
        { 
            get => _shopFee; 
            set 
            { 
                _shopFee = value; 
                OnPropertyChanged(nameof(ShopFee));
                OnPropertyChanged(nameof(HasShopFee));
            } 
        }
        
        private decimal _deliveryCharges;
        public decimal DeliveryCharges 
        { 
            get => _deliveryCharges; 
            set 
            { 
                _deliveryCharges = value; 
                OnPropertyChanged(nameof(DeliveryCharges));
                OnPropertyChanged(nameof(HasDeliveryCharges));
            } 
        }
        
        private decimal _rewardDiscount;
        public decimal RewardDiscount 
        { 
            get => _rewardDiscount; 
            set 
            { 
                _rewardDiscount = value; 
                OnPropertyChanged(nameof(RewardDiscount));
                OnPropertyChanged(nameof(HasRewardDiscount));
            } 
        }
        
        private decimal _tips;
        public decimal Tips 
        { 
            get => _tips; 
            set 
            { 
                _tips = value; 
                OnPropertyChanged(nameof(Tips));
                OnPropertyChanged(nameof(HasTips));
            } 
        }
        
        private decimal _tipPercentage;
        public decimal TipPercentage 
        { 
            get => _tipPercentage; 
            set 
            { 
                _tipPercentage = value; 
                OnPropertyChanged(nameof(TipPercentage));
            } 
        }
        
        // Visibility properties for conditional display
        public bool HasBogoDiscount => BogoDiscount > 0;
        public bool HasShopFee => ShopFee > 0;
        public bool HasDeliveryCharges => DeliveryCharges > 0;
        public bool HasRewardDiscount => RewardDiscount > 0;
        public bool HasTips => Tips > 0;
        
        // Shop fees display (from order details API)
        public ObservableCollection<ShopFeeDisplayModel> ShopFeeRows { get; set; } = new ObservableCollection<ShopFeeDisplayModel>();
        public bool HasShopFees => ShopFeeRows.Count > 0;
        public decimal TotalShopFees => ShopFeeRows.Sum(f => f.Amount);
        
        public bool ShowContactAccessCode
        {
            get
            {
                var hasCode = !string.IsNullOrWhiteSpace(ContactAccessCode);
                if (!hasCode) return false;
                var isUber = string.Equals(Platform, "Uber Eats", StringComparison.OrdinalIgnoreCase);
                return isUber || PlatformId == 2;
            }
        }
        
        public ICommand CloseCommand { get; set; }
        public ICommand AcceptCommand { get; set; }
        public ICommand RejectCommand { get; set; }
        public event Action DialogClosed;

        // Incoming order's order_session_id
        public int? OrderSessionId { get; set; }

        // Properties for storing selected table information
        public int? SelectedTableId { get; set; }
        /// <summary>Table orderings ID for the selected table (used when updating status to RESERVED).</summary>
        public int? SelectedTableOrderingsId { get; set; }
        public string TableId { get; set; }
        private string _selectedTableName;
        public string SelectedTableName
        {
            get => _selectedTableName;
            set
            {
                _selectedTableName = value;
                OnPropertyChanged(nameof(SelectedTableName));
                OnPropertyChanged(nameof(HasTableName));
            }
        }

        // Derived properties for table orders
        public bool IsTableOrder 
        { 
            get => string.Equals(Platform, "Table order", StringComparison.OrdinalIgnoreCase); 
        }
        public bool HasTableName => !string.IsNullOrWhiteSpace(SelectedTableName);

   
        private int _isTableOrderFlag;
        public int IsTableOrderFlag
        {
            get => _isTableOrderFlag;
            set
            {
                _isTableOrderFlag = value;
                OnPropertyChanged(nameof(IsTableOrderFlag));
            }
        }

        private bool _isAccepting;
        public bool IsAccepting
        {
            get => _isAccepting;
            set
            {
                _isAccepting = value;
                OnPropertyChanged(nameof(IsAccepting));
            }
        }

        private bool _isRejecting;
        public bool IsRejecting
        {
            get => _isRejecting;
            set
            {
                _isRejecting = value;
                OnPropertyChanged(nameof(IsRejecting));
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged(nameof(IsLoading));
            }
        }

        public OrderDetailsDialogViewModel()
        {
            CloseCommand = new RelayCommand(() => { OnDialogClosed(); });
            // AcceptCommand and RejectCommand will be set by GlobalDataService when showing the dialog
        }

        public virtual void OnDialogClosed()
        {
            DialogClosed?.Invoke();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public class ShopFeeDisplayModel
        {
            public string Label { get; set; }
            public decimal Amount { get; set; }
        }

        public static OrderDetailsDialogViewModel CreateSample()
        {
            var vm = new OrderDetailsDialogViewModel
            {
                OrderNumber = "APC356",
                OrderTypeTime = "Collection - Pickup at : 03:00 pm",
                Contact = "+94767886124",
                OrderNotes = "Make it less spicy",
                DeliveryType = "Collection",
                CustomerName = "John Doe",
                PaymentStatus = "UNPAID",
                Subtotal = 0m,
                Total = 0m,
                BogoDiscount = 0m,
                ShopFee = 0m,
                DeliveryCharges = 0m,
                RewardDiscount = 0m,
                Tips = 0m,
                TipPercentage = 0m
            };
            vm.Items.Add(new OrderItemViewModel
            {
                Quantity = 1,
                Name = "Chicken Cheese Kottu",
                Notes = "Less Spicy and send some tissues!",
                Price = 0m
            });
            vm.Items.Add(new OrderItemViewModel
            {
                Quantity = 2,
                Name = "Chicken Submarine",
                Notes = "Less Spicy and send some tissues!",
                Price = 0m
            });
            return vm;
        }
    }


    public class OrderItemViewModel : INotifyPropertyChanged
    {
        public int Quantity { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        // API provided total for this item (already includes modifiers); used for incoming order modal display
        public decimal ApiItemTotal { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountAmount { get; set; }
        public List<ModifierDetailModel> ModifierDetailsForDisplay { get; set; } = new List<ModifierDetailModel>();
        public List<Models.PrinterGroupModel> PrinterGroups { get; set; } = new List<Models.PrinterGroupModel>();
        public decimal Total => Price * Quantity;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 