using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class CartService : INotifyPropertyChanged
    {
        private static CartService _instance;
        public static CartService Instance => _instance ??= new CartService();

        private readonly TaxCalculationService _taxCalculationService = new TaxCalculationService();
        public CartTaxResult CurrentTaxResult { get; private set; } = new CartTaxResult();
        public event Action<CartTaxResult> TaxesUpdated;
        private readonly HashSet<int> _removedOptionalShopFeeIds = new HashSet<int>();
        private readonly HashSet<string> _removedOptionalShopFeeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Expose removed shop fees for draft saving
        public List<int> GetRemovedShopFeeIds() => _removedOptionalShopFeeIds.ToList();
        public List<string> GetRemovedShopFeeNames() => _removedOptionalShopFeeNames.ToList();
        
        // Restore removed shop fees when loading draft
        public void RestoreRemovedShopFees(List<int> removedIds, List<string> removedNames)
        {
            _removedOptionalShopFeeIds.Clear();
            _removedOptionalShopFeeNames.Clear();
            
            if (removedIds != null)
            {
                foreach (var id in removedIds)
                {
                    _removedOptionalShopFeeIds.Add(id);
                }
            }
            
            if (removedNames != null)
            {
                foreach (var name in removedNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _removedOptionalShopFeeNames.Add(name.Trim());
                    }
                }
            }
            
            RecalculateTaxes();
        }

        private CartService()
        {
            OrderItems = new ObservableCollection<OrderItem>();
            OrderItems.CollectionChanged += (s, e) =>
            {
                if (OrderItems.Count == 0)
                {
                    _removedOptionalShopFeeIds.Clear();
                    _removedOptionalShopFeeNames.Clear();
                }

                InvalidateApiTotalOverrides();

                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(ItemsDiscountTotal));
                OnPropertyChanged(nameof(ItemsSubTotal));
                OnPropertyChanged(nameof(SubTotal));
                OnPropertyChanged(nameof(GrandTotal));
                OnPropertyChanged(nameof(EffectiveTotalForPayment));
                OnPropertyChanged(nameof(ItemCount));
                RecalculateTaxes();
                // Recalculate voucher discount if subtotal changed and voucher is percentage-based
                RecalculateVoucherDiscount();
            };

            RecalculateTaxes();
        }

        private void OnOrderItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Update cart totals when any item property changes
            if (e.PropertyName == nameof(OrderItem.Quantity) || 
                e.PropertyName == nameof(OrderItem.Price) || 
                e.PropertyName == nameof(OrderItem.Total) ||
                e.PropertyName == nameof(OrderItem.DiscountAmount) ||
                e.PropertyName == nameof(OrderItem.DiscountPercent))
            {
                InvalidateApiTotalOverrides();
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(ItemsDiscountTotal));
                OnPropertyChanged(nameof(SubTotal));
                RecalculateTaxes();
                // Recalculate voucher discount if subtotal changed and voucher is percentage-based
                RecalculateVoucherDiscount();
            }
        }

        public ObservableCollection<OrderItem> OrderItems { get; private set; }

        // Order context properties with backing fields and notifications
        private string _orderType = "Take Away";
        public string OrderType
        {
            get => _orderType;
            set
            {
                _orderType = value;
                InvalidateApiTotalOverrides();
                OnPropertyChanged();
                RecalculateTaxes();
            }
        }

        private string _customerName;
        private string _lastCustomerName;
        public string CustomerName
        {
            get => _customerName;
            set 
            { 
                _customerName = value; 
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _lastCustomerName = value.Trim();
                }
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(LastCustomerName));
            }
        }
        public string LastCustomerName => _lastCustomerName;

        private string _customerPhone;
        private string _lastCustomerPhone;
        public string CustomerPhone
        {
            get => _customerPhone;
            set 
            { 
                _customerPhone = value; 
                if (!string.IsNullOrWhiteSpace(value))
                {
                    _lastCustomerPhone = value.Trim();
                }
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(LastCustomerPhone));
            }
        }
        public string LastCustomerPhone => _lastCustomerPhone;

        // Track the currently loaded order's API id and customer id for update scenarios
        public int? CurrentOrderApiId { get; private set; }
        public int? CurrentCustomerId { get; private set; }

        private int? _tableNumber;
        public int? TableNumber
        {
            get => _tableNumber;
            set { _tableNumber = value; OnPropertyChanged(); }
        }

        private string _tableName;
        public string TableName
        {
            get => _tableName;
            set { _tableName = value; OnPropertyChanged(); }
        }

        private string _note;
        public string Note
        {
            get => _note;
            set
            {
                // Enforce max length of 100 characters for cart notes
                var normalized = value;
                if (!string.IsNullOrEmpty(normalized) && normalized.Length > 100)
                {
                    normalized = normalized.Substring(0, 100);
                }
                _note = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNote));
            }
        }
        public bool HasNote => !string.IsNullOrWhiteSpace(Note);

        private decimal _discountAmount;
        public decimal DiscountAmount
        {
            get => _discountAmount;
            set
            {
                _discountAmount = value;
                InvalidateApiTotalOverrides();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubTotal));
                RecalculateTaxes();
            }
        }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = value; OnPropertyChanged(); }
        }

        private string _discountDescription;
        public string DiscountDescription
        {
            get => _discountDescription;
            set { _discountDescription = value; OnPropertyChanged(); }
        }

        private string _couponCode;
        public string CouponCode
        {
            get => _couponCode;
            set
            {
                _couponCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasCoupon));
            }
        }
        public bool HasCoupon => !string.IsNullOrWhiteSpace(CouponCode);

        private decimal _couponAmount;
        public decimal CouponAmount
        {
            get => _couponAmount;
            set
            {
                _couponAmount = value;
                InvalidateApiTotalOverrides();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubTotal));
                RecalculateTaxes();
            }
        }

        private decimal _voucherDiscount;
        public decimal VoucherDiscount
        {
            get => _voucherDiscount;
            set
            {
                _voucherDiscount = value;
                OnPropertyChanged();
            }
        }

        private List<Models.VoucherModel> _vouchers = new List<Models.VoucherModel>();
        public List<Models.VoucherModel> Vouchers
        {
            get => _vouchers;
            set
            {
                _vouchers = value ?? new List<Models.VoucherModel>();
                OnPropertyChanged();
            }
        }

        private decimal _deliveryCharge;
        public decimal DeliveryCharge
        {
            get => _deliveryCharge;
            set
            {
                _deliveryCharge = value;
                InvalidateApiTotalOverrides();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubTotal));
                RecalculateTaxes();
            }
        }

        // Delivery address for Delivery orders
        private string _deliveryAddress;
        public string DeliveryAddress
        {
            get => _deliveryAddress;
            set { _deliveryAddress = value; OnPropertyChanged(); }
        }

        private string _couponDescription;
        public string CouponDescription
        {
            get => _couponDescription;
            set { _couponDescription = value; OnPropertyChanged(); }
        }

        private string _displayOrderId;
        public string DisplayOrderId
        {
            get => _displayOrderId;
            set { _displayOrderId = value; OnPropertyChanged(); }
        }

        // Cashier UI order code for the current in-progress session. Survives navigating away from Cashier
        private string _cashierSessionDisplayOrderId;
        public string CashierSessionDisplayOrderId
        {
            get => _cashierSessionDisplayOrderId;
            set { _cashierSessionDisplayOrderId = value; OnPropertyChanged(); }
        }

        private string _deliveryPlatformName;
        public string DeliveryPlatformName
        {
            get => _deliveryPlatformName;
            set { _deliveryPlatformName = value; OnPropertyChanged(); }
        }

        private DateTime? _orderCreatedAt;
        public DateTime? OrderCreatedAt
        {
            get => _orderCreatedAt;
            set { _orderCreatedAt = value; OnPropertyChanged(); }
        }

        private DateTime? _pickupTime;
        public DateTime? PickupTime
        {
            get => _pickupTime;
            set { _pickupTime = value; OnPropertyChanged(); }
        }

        private DateTime? _selectedOrderTime;
        public DateTime? SelectedOrderTime
        {
            get => _selectedOrderTime;
            set { _selectedOrderTime = value; OnPropertyChanged(); }
        }

        // If we have API-provided totals on the loaded order, prefer them over recalculation until the cart is edited
        private decimal? _apiTotalOverride;
        private decimal? _apiSubTotalOverride;

        /// <summary>Clears API snapshot totals when line items or payment-affecting fields change so <see cref="EffectiveTotalForPayment"/> matches recalculated <see cref="SubTotal"/>.</summary>
        private void InvalidateApiTotalOverrides()
        {
            if (_apiTotalOverride == null && _apiSubTotalOverride == null)
                return;
            _apiTotalOverride = null;
            _apiSubTotalOverride = null;
            OnPropertyChanged(nameof(ApiTotal));
            OnPropertyChanged(nameof(ApiSubTotal));
            OnPropertyChanged(nameof(EffectiveTotalForPayment));
        }
        // Raw items sum (no discounts)
        public decimal Total => OrderItems.Sum(i => i.Price * i.Quantity);
        // Sum of all per-item discounts (from percent or API-provided amount)
        public decimal ItemsDiscountTotal => OrderItems.Sum(i => i.DiscountAmount);
        // Items subtotal (sum of item totals; item discounts already reflected in item.Price)
        public decimal ItemsSubTotal => Total;
        // Calculated shop fees from GlobalDataService.ShopDetails.ShopFees
        public decimal CalculatedShopFees
        {
            get
            {
                try
                {
                    if (OrderItems == null || OrderItems.Count == 0) return 0m;
                    var shop = GlobalDataService.Instance.ShopDetails;
                    if (shop?.ShopFees == null || shop.ShopFees.Count == 0) return 0m;

                    var baseAmount = Total - DiscountAmount - CouponAmount; // fees applied on subtotal before delivery
                    if (baseAmount < 0) baseAmount = 0;

                    var orderTypeKey = NormalizeOrderTypeKey(OrderType);

                    decimal sum = 0m;
                    foreach (var fee in shop.ShopFees)
                    {
                        if (fee == null) continue;

                        var feeTypeKey = NormalizeOrderTypeKey(fee.Type);
                        if (!string.IsNullOrEmpty(feeTypeKey) && !string.Equals(feeTypeKey, orderTypeKey, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!fee.Mandatory)
                        {
                            if ((fee.Id > 0 && _removedOptionalShopFeeIds.Contains(fee.Id)) ||
                                (!string.IsNullOrWhiteSpace(fee.FeeName) && _removedOptionalShopFeeNames.Contains(fee.FeeName.Trim())))
                            {
                                continue;
                            }
                        }

                        var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                        if (type == "PERCENTAGE")
                        {
                            sum += Math.Round(baseAmount * (fee.Fee / 100m), 2, MidpointRounding.AwayFromZero);
                        }
                        else if (type == "VALUE")
                        {
                            sum += fee.Fee;
                        }
                    }
                    return sum;
                }
                catch { return 0m; }
            }
        }

        // Calculate discounted delivery charge when discount or coupon is applied
       /* private decimal GetDiscountedDeliveryCharge()
        {
            if (DeliveryCharge <= 0m) return 0m;
            
            // Calculate total discount (discount + coupon)
            decimal totalDiscount = DiscountAmount + CouponAmount;
            if (totalDiscount <= 0m) return DeliveryCharge;
            
            // Calculate items subtotal before discount
            decimal itemsSubtotalBeforeDiscount = Total;
            if (itemsSubtotalBeforeDiscount <= 0m) return DeliveryCharge;
            
            // Calculate discount percentage applied to items
            decimal discountPercentage = totalDiscount / itemsSubtotalBeforeDiscount;
            
            // Apply the same percentage discount to delivery charge
            decimal deliveryDiscount = Math.Round(DeliveryCharge * discountPercentage, 2, MidpointRounding.AwayFromZero);
            decimal discountedDeliveryCharge = Math.Max(0m, DeliveryCharge - deliveryDiscount);
            
            return discountedDeliveryCharge;
        }
        */
        // Grand total shown in cart: items subtotal minus cart-level discount/coupon plus discounted delivery plus shop fees
        public decimal SubTotal => (Total - DiscountAmount - CouponAmount + DeliveryCharge + CalculatedShopFees);
        // Alias for clarity; used for payment and API total_amount
        public decimal GrandTotal => SubTotal;

        // Expose API-provided totals (when loaded from API) for payment reconciliation
        public decimal? ApiTotal => _apiTotalOverride;
        public decimal? ApiSubTotal => _apiSubTotalOverride;
        // Preferred total to use when collecting payment (falls back to calculated SubTotal)
        public decimal EffectiveTotalForPayment => _apiTotalOverride ?? SubTotal;
        public int ItemCount => OrderItems.Count;

        // NEW: Create OrderModel from current state
        public OrderModel CreateOrderModel(string displayOrderId, CustomerModel selectedCustomer = null, decimal discountPercentage = 0, CustomerAddressModel selectedAddress = null)
        {
            return OrderModel.FromCartService(this, displayOrderId, selectedCustomer, discountPercentage, selectedAddress);
        }

        // NEW: Load from OrderModel
        public void LoadFromOrderModel(OrderModel order)
        {
            // Clear current state
            ClearCart();
            
            // Load from order
            CustomerName = order.CustomerName;
            CustomerPhone = order.CustomerPhone;
            // Use API-provided table id (stored in OrderModel.TableNumber) when loading from API
            TableNumber = order.TableNumber;
            Note = order.OrderNotes;
            // Respect order-level discount from API
            DiscountAmount = order.DiscountAmount;
            DiscountPercent = order.DiscountPercentage;
            DiscountDescription = order.DiscountDescription;
            CouponCode = order.CouponCode;
            CouponAmount = order.CouponAmount > 0 ? order.CouponAmount : order.VoucherDiscount;
            // Store voucher discount separately for display purposes
            VoucherDiscount = order.VoucherDiscount > 0 ? order.VoucherDiscount : 0;
            Vouchers = order.Vouchers?.ToList() ?? new List<Models.VoucherModel>();
            
            // If vouchers exist but CouponCode is not set, set it from the first voucher's code
            // This ensures voucher details are displayed in the cart when loading an order
            if (Vouchers != null && Vouchers.Count > 0 && string.IsNullOrWhiteSpace(CouponCode))
            {
                var firstVoucher = Vouchers.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.VoucherCode));
                if (firstVoucher != null)
                {
                    CouponCode = firstVoucher.VoucherCode;
                    // If CouponAmount is 0 but voucher has discount, use voucher discount
                    if (CouponAmount == 0 && firstVoucher.VoucherDiscount > 0)
                    {
                        CouponAmount = firstVoucher.VoucherDiscount;
                    }
                    // Also ensure VoucherDiscount is set if it's not already set
                    if (VoucherDiscount == 0 && firstVoucher.VoucherDiscount > 0)
                    {
                        VoucherDiscount = firstVoucher.VoucherDiscount;
                    }
                    
                    // Set coupon description based on voucher details (voucher_value and value_type)
                    // This matches the format used in SetCouponDialog
                    string couponDescription = $"Coupon ({firstVoucher.VoucherCode})";
                    if (!string.IsNullOrEmpty(firstVoucher.VoucherValue) && decimal.TryParse(firstVoucher.VoucherValue, out decimal voucherValue))
                    {
                        if (firstVoucher.ValueType?.ToLower() == "percentage")
                        {
                            // Percentage discount
                            couponDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue}%";
                        }
                        else
                        {
                            // Fixed amount discount
                            couponDescription = $"Coupon ({firstVoucher.VoucherCode}) - {voucherValue:C}";
                        }
                    }
                    CouponDescription = couponDescription;
                }
            }
            // If CouponCode is already set but CouponDescription is not, try to set it from vouchers
            else if (!string.IsNullOrWhiteSpace(CouponCode) && string.IsNullOrWhiteSpace(CouponDescription) && Vouchers != null && Vouchers.Count > 0)
            {
                var matchingVoucher = Vouchers.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.VoucherCode) && 
                    v.VoucherCode.Equals(CouponCode, StringComparison.OrdinalIgnoreCase));
                if (matchingVoucher != null)
                {
                    // Set coupon description based on voucher details
                    string couponDescription = $"Coupon ({matchingVoucher.VoucherCode})";
                    if (!string.IsNullOrEmpty(matchingVoucher.VoucherValue) && decimal.TryParse(matchingVoucher.VoucherValue, out decimal voucherValue))
                    {
                        if (matchingVoucher.ValueType?.ToLower() == "percentage")
                        {
                            // Percentage discount
                            couponDescription = $"Coupon ({matchingVoucher.VoucherCode}) - {voucherValue}%";
                        }
                        else
                        {
                            // Fixed amount discount
                            couponDescription = $"Coupon ({matchingVoucher.VoucherCode}) - {voucherValue:C}";
                        }
                    }
                    CouponDescription = couponDescription;
                }
            }
            DisplayOrderId = order.DisplayOrderId;
            OrderCreatedAt = order.CreatedAt;
            PickupTime = order.ScheduledTime; // Load pickup time from order if available
            SelectedOrderTime = order.ScheduledTime; // Also set SelectedOrderTime for consistency
            DeliveryPlatformName = order?.DeliveryPlatfornName;
            
            // Set order type
            OrderType = order.OrderType switch
            {
                Models.OrderType.DineIn => "Dine In",
                Models.OrderType.TakeAway => "Take Away",
                Models.OrderType.Delivery => "Delivery",
                _ => "Take Away"
            };

            // Remember API id for subsequent update
            CurrentOrderApiId = order.ApiId;
            // Remember customer id
            CurrentCustomerId = order.CustomerId;

            // Load items; if API provided a per-item price, normalize to unit price when needed
            foreach (var item in order.Items)
            {
                if (item.ApiItemPrice > 0)
                {
                    var apiPrice = item.ApiItemPrice;
                    // Some APIs return line total as ApiItemPrice. If so, divide by quantity to get unit price.
                    if (item.Quantity > 0 && apiPrice > 0)
                    {
                        var unit = Math.Round(apiPrice / item.Quantity, 2, MidpointRounding.AwayFromZero);
                        // Heuristic: if quantity > 1 and unit * qty reconstructs the api price, treat as line total
                        if (item.Quantity > 1 && Math.Round(unit * item.Quantity, 2, MidpointRounding.AwayFromZero) == Math.Round(apiPrice, 2, MidpointRounding.AwayFromZero))
                        {
                            item.Price = unit;
                        }
                        else
                        {
                            item.Price = apiPrice;
                        }
                    }
                    else
                    {
                        item.Price = apiPrice;
                    }
                }
                // Fix for item discount display on order load
                // User requirement: When loading an order (e.g. Dine In update), the discount often comes in as the UNIT discount 
                // but needs to be displayed as the LINE total.
                // Example: Unit discount 0.2, Qty 3 -> Cart should show 0.6 (0.2 * 3).
                // We multiply source discount by quantity to get the correct line total.
                if (item.Quantity > 0)
                {
                    var sourceDiscount = item.DisAmount > 0m ? item.DisAmount : item.ApiDiscountAmount;
                    if (sourceDiscount > 0m)
                    {
                        // Treat sourceDiscount as the UNIT discount amount
                        var lineTotal = Math.Round(sourceDiscount * item.Quantity, 2, MidpointRounding.AwayFromZero);
                        
                        item.VisibleDiscountAmount = lineTotal;
                        
                        // Sync other fields to the line total
                        item.DisAmount = lineTotal/item.Quantity;
                        item.ApiDiscountAmount = lineTotal/item.Quantity;
                        
                        // Note: VisibleDiscountAmount setter automatically updates UnitDiscountAmount 
                        // to (lineTotal / Quantity), which will correctly equal the original sourceDiscount.
                    }
                }
                // Subscribe to property changes on the item to update cart totals
                OrderItemTaxComponentBuilder.EnsureComponents(item);
                item.PropertyChanged += OnOrderItemPropertyChanged;
                OrderItems.Add(item);
            }

            // Prefer API totals after items are loaded so CollectionChanged invalidation does not clear them
            _apiSubTotalOverride = order.ApiSubTotal;
            _apiTotalOverride = order.ApiTotal;
            OnPropertyChanged(nameof(EffectiveTotalForPayment));
        }

        // Cart Operations
        public void AddItem(OrderItem item)
        {
            var existing = OrderItems.FirstOrDefault(i => ItemsRepresentSameLine(i, item));
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                // Subscribe to property changes on the item to update cart totals
                OrderItemTaxComponentBuilder.EnsureComponents(item);
                item.PropertyChanged += OnOrderItemPropertyChanged;
                OrderItems.Add(item);
            }
        }

        // Add item as a separate line without merging (used when editing existing orders)
        public void AddItemAsSeparateLine(OrderItem item)
        {
            // Subscribe to property changes on the item to update cart totals
            OrderItemTaxComponentBuilder.EnsureComponents(item);
            item.PropertyChanged += OnOrderItemPropertyChanged;
            OrderItems.Add(item);
        }

        public void AddItemFromDraft(OrderItem item)
        {
            // Add item directly without checking for duplicates (for draft loading)
            // Note: DisAmount, UnitDiscountAmount, and VisibleDiscountAmount are already set correctly
            // in CashierHomeViewModel when loading from draft, so we don't need to recalculate them here
            // DisAmount = total discount for the line
            // UnitDiscountAmount = per-unit discount (DisAmount / Quantity)
            // VisibleDiscountAmount = total discount for display (same as DisAmount)
            
            OrderItemTaxComponentBuilder.EnsureComponents(item);
            item.PropertyChanged += OnOrderItemPropertyChanged;
            OrderItems.Add(item);
        }

        private bool ModifiersEqual(Dictionary<int, List<string>> a, Dictionary<int, List<string>> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var bList)) return false;
                var aList = kvp.Value ?? new List<string>();
                bList = bList ?? new List<string>();
                if (aList.Count != bList.Count) return false;
                if (!aList.OrderBy(x => x).SequenceEqual(bList.OrderBy(x => x))) return false;
            }
            return true;
        }

        private bool NestedModifiersEqual(Dictionary<string, List<string>> a, Dictionary<string, List<string>> b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null) return a == b;
            if (a.Count != b.Count) return false;
            foreach (var kvp in a)
            {
                if (!b.TryGetValue(kvp.Key, out var bList)) return false;
                var aList = kvp.Value ?? new List<string>();
                bList = bList ?? new List<string>();
                if (aList.Count != bList.Count) return false;
                if (!aList.OrderBy(x => x).SequenceEqual(bList.OrderBy(x => x))) return false;
            }
            return true;
        }

		private bool ItemsRepresentSameLine(OrderItem a, OrderItem b)
		{
			if (a == null || b == null) return false;
			if (a.IsReadOnly || b.IsReadOnly) return false;

			// Prefer stable product identity when available
			var aProductId = a.Product?.Id ?? 0;
			var bProductId = b.Product?.Id ?? 0;
			if (aProductId > 0 || bProductId > 0)
			{
				// If both have a product id, they must match
				if (aProductId <= 0 || bProductId <= 0) return false;
				if (aProductId != bProductId) return false;
			}
			else
			{
				// Fallback to display name comparison when product id is unavailable
				var aName = (a.Product?.ItemName ?? a.Name ?? string.Empty).Trim();
				var bName = (b.Product?.ItemName ?? b.Name ?? string.Empty).Trim();
				if (!string.Equals(aName, bName, StringComparison.OrdinalIgnoreCase)) return false;
			}

			// Modifiers must match
			if (!ModifiersEqual(a.SelectedModifiers, b.SelectedModifiers)) return false;
			if (!NestedModifiersEqual(a.NestedModifierDetails, b.NestedModifierDetails)) return false;

			// Notes must match exactly (normalized to empty string)
			if ((a.Note ?? string.Empty) != (b.Note ?? string.Empty)) return false;

			// Keep discount equivalence to avoid merging items with different discounts
			if (a.DisAmount != b.ApiDiscountAmount) return false;

			// Do not require price equality; identical products with same modifiers may have rounding differences
			return true;
		}

        public void RemoveItem(OrderItem item)
        {
            // Unsubscribe from property changes before removing
            item.PropertyChanged -= OnOrderItemPropertyChanged;
            OrderItems.Remove(item);
        }

        public void UpdateItemQuantity(OrderItem item, int newQuantity)
        {
            if (newQuantity <= 0)
                RemoveItem(item);
            else
                item.Quantity = newQuantity;
        }

        public void ClearCart()
        {
            // Unsubscribe from all items before clearing
            foreach (var item in OrderItems)
            {
                item.PropertyChanged -= OnOrderItemPropertyChanged;
            }
            
            OrderItems.Clear();
            _removedOptionalShopFeeIds.Clear();
            _removedOptionalShopFeeNames.Clear();
            Note = null;
            DiscountAmount = 0;
            DiscountPercent = 0;
            DiscountDescription = null;
            CouponCode = null;
            CouponAmount = 0;
            CouponDescription = null;
            VoucherDiscount = 0;
            Vouchers.Clear();
            DeliveryCharge = 0;
            DeliveryAddress = null;
            CustomerName = null;
            CustomerPhone = null;
            TableNumber = null;
            DisplayOrderId = null;
            OrderCreatedAt = null;
            PickupTime = null;
            SelectedOrderTime = null;
            _apiTotalOverride = null;
            _apiSubTotalOverride = null;
            CurrentOrderApiId = null;
            CurrentCustomerId = null;
            RecalculateTaxes();
        }

        // Explicitly clear remembered last-customer details so future screens default to Guest/customer list default
        public void ResetCustomerHistory()
        {
            _lastCustomerName = null;
            _lastCustomerPhone = null;
            OnPropertyChanged(nameof(LastCustomerName));
            OnPropertyChanged(nameof(LastCustomerPhone));
        }

        public void ApplyDiscount(decimal amount, string description = null)
        {
            DiscountAmount = amount;
            DiscountDescription = description ?? $"Discount: {amount:C}";
        }

        public void RemoveDiscount()
        {
            DiscountAmount = 0;
            DiscountDescription = null;
        }

        public void ApplyCoupon(string code, decimal amount, string description = null)
        {
            CouponCode = code;
            CouponAmount = amount;
            CouponDescription = description ?? $"Coupon: {code}";
        }

        public void ApplyVoucher(Models.VoucherModel voucher)
        {
            if (voucher != null)
            {
                // Remove existing voucher with same code if any
                Vouchers.RemoveAll(v => v.VoucherCode == voucher.VoucherCode);
                Vouchers.Add(voucher);
                // Also update coupon code/amount for backward compatibility
                CouponCode = voucher.VoucherCode;
                CouponAmount = voucher.VoucherDiscount;
            }
        }

        public void RemoveCoupon()
        {
            CouponCode = null;
            CouponAmount = 0;
            CouponDescription = null;
            Vouchers.Clear();
        }

        /// <summary>
        /// Recalculates voucher discount when subtotal changes, if the voucher is percentage-based.
        /// This is called automatically when order items change.
        /// </summary>
        private void RecalculateVoucherDiscount()
        {
            // Only recalculate if there are vouchers
            if (Vouchers == null || Vouchers.Count == 0)
            {
                return;
            }

            // Find percentage-based vouchers
            foreach (var voucher in Vouchers)
            {
                // Check if voucher is percentage type
                if (voucher != null &&
                    !string.IsNullOrWhiteSpace(voucher.ValueType) &&
                    voucher.ValueType.Equals("percentage", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(voucher.VoucherValue) &&
                    decimal.TryParse(voucher.VoucherValue, out var voucherPercent) &&
                    voucherPercent > 0 && voucherPercent <= 100)
                {
                    // Recalculate voucher discount based on (Total - DiscountAmount)
                    // Apply coupon to the amount after discount (subtotal - discount amount)
                    var baseAmount = Total - DiscountAmount;
                    if (baseAmount < 0) baseAmount = 0;
                    var newVoucherDiscount = Math.Round(baseAmount * voucherPercent / 100m, 2, MidpointRounding.AwayFromZero);
                    
                    // Update voucher discount in the voucher model
                    voucher.VoucherDiscount = newVoucherDiscount;
                    
                    // Update CartService VoucherDiscount property
                    VoucherDiscount = newVoucherDiscount;
                    
                    // Also update CouponAmount if this voucher is the active coupon
                    if (!string.IsNullOrWhiteSpace(CouponCode) &&
                        !string.IsNullOrWhiteSpace(voucher.VoucherCode) &&
                        CouponCode.Equals(voucher.VoucherCode, StringComparison.OrdinalIgnoreCase))
                    {
                        CouponAmount = newVoucherDiscount;
                    }
                    
                    // Notify property changes
                    OnPropertyChanged(nameof(VoucherDiscount));
                    OnPropertyChanged(nameof(SubTotal));
                }
            }
        }

        public void RemoveShopFee(int shopFeeId, string name = null, bool isMandatory = false)
        {
            if (isMandatory) return;
            _removedOptionalShopFeeIds.Add(shopFeeId);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _removedOptionalShopFeeNames.Add(name.Trim());
            }
            InvalidateApiTotalOverrides();
            RecalculateTaxes();
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }

        /// <summary>Re-applies an optional shop fee after the user chose to hide it in the cart UI.</summary>
        public void RestoreShopFee(int shopFeeId, string name = null)
        {
            if (shopFeeId > 0)
                _removedOptionalShopFeeIds.Remove(shopFeeId);
            if (!string.IsNullOrWhiteSpace(name))
                _removedOptionalShopFeeNames.Remove(name.Trim());
            InvalidateApiTotalOverrides();
            RecalculateTaxes();
            OnPropertyChanged(nameof(SubTotal));
            OnPropertyChanged(nameof(GrandTotal));
        }

        /// <summary>All applicable shop fee rows for the cart list, including optional fees marked removed (still shown faded).</summary>
        public List<ShopFeeCartDisplayRow> GetShopFeeRowsForDisplay()
        {
            var result = new List<ShopFeeCartDisplayRow>();
            try
            {
                if (OrderItems == null || OrderItems.Count == 0) return result;
                var shop = GlobalDataService.Instance.ShopDetails;
                if (shop?.ShopFees == null || shop.ShopFees.Count == 0) return result;

                var feeBase = Total - DiscountAmount - CouponAmount;
                if (feeBase < 0) feeBase = 0;
                var orderTypeKey = NormalizeOrderTypeKey(OrderType);

                foreach (var fee in shop.ShopFees)
                {
                    if (fee == null) continue;
                    var feeTypeKey = NormalizeOrderTypeKey(fee.Type);
                    if (!string.IsNullOrEmpty(feeTypeKey) && !string.Equals(feeTypeKey, orderTypeKey, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                    decimal amount = 0m;
                    if (type == "PERCENTAGE")
                        amount = Math.Round(feeBase * (fee.Fee / 100m), 2, MidpointRounding.AwayFromZero);
                    else if (type == "VALUE")
                        amount = fee.Fee;

                    if (amount <= 0) continue;

                    var isRemoved = !fee.Mandatory &&
                        ((fee.Id > 0 && _removedOptionalShopFeeIds.Contains(fee.Id)) ||
                         (!string.IsNullOrWhiteSpace(fee.FeeName) && _removedOptionalShopFeeNames.Contains(fee.FeeName.Trim())));

                    result.Add(new ShopFeeCartDisplayRow
                    {
                        ShopFeeId = fee.Id,
                        Name = fee.FeeName,
                        Amount = amount,
                        IsMandatory = fee.Mandatory,
                        IsRemoved = isRemoved,
                        FeeType = fee.FeeType,
                        FeeValue = fee.Fee
                    });
                }
            }
            catch
            {
                // Same tolerance as GetCalculatedShopFees
            }

            return result;
        }

        public List<OrderShopFeeModel> GetCalculatedShopFees()
        {
            var result = new List<OrderShopFeeModel>();
            try
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                if (shop?.ShopFees == null || shop.ShopFees.Count == 0) return result;

                var feeBase = Total - DiscountAmount - CouponAmount;
                if (feeBase < 0) feeBase = 0;

                var orderTypeKey = NormalizeOrderTypeKey(OrderType);

                foreach (var fee in shop.ShopFees)
                {
                    if (fee == null) continue;
                    var feeTypeKey = NormalizeOrderTypeKey(fee.Type);
                    if (!string.IsNullOrEmpty(feeTypeKey) && !string.Equals(feeTypeKey, orderTypeKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (!fee.Mandatory)
                    {
                        if ((fee.Id > 0 && _removedOptionalShopFeeIds.Contains(fee.Id)) ||
                            (!string.IsNullOrWhiteSpace(fee.FeeName) && _removedOptionalShopFeeNames.Contains(fee.FeeName.Trim())))
                        {
                            continue;
                        }
                    }
                    var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                    decimal amount = 0m;
                    if (type == "PERCENTAGE")
                    {
                        amount = Math.Round(feeBase * (fee.Fee / 100m), 2, MidpointRounding.AwayFromZero);
                    }
                    else if (type == "VALUE")
                    {
                        amount = fee.Fee;
                    }

                    if (amount <= 0) continue;

                    result.Add(new OrderShopFeeModel
                    {
                        ShopFeeId = fee.Id,
                        Type = fee.Type,
                        Name = fee.FeeName,
                        Amount = amount,
                        FeeType = fee.FeeType,
                        FeeValue = fee.Fee,
                        IsMandatory = fee.Mandatory,
                        TaxId = fee.TaxId,
                        TaxProfileId = fee.TaxProfileId,
                        TaxCode = fee.TaxCode,
                        TaxRate = fee.TaxRate
                    });
                }
            }
            catch
            {
                // Ignore shop fee calculation errors
            }

            return result;
        }

        public void ForceRecalculateTaxes()
        {
            RecalculateTaxes();
        }

        private void RecalculateTaxes()
        {
            try
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                var taxResult = _taxCalculationService.Calculate(this, shop, OrderType ?? "Take Away");
                CurrentTaxResult = taxResult ?? new CartTaxResult();
                TaxesUpdated?.Invoke(CurrentTaxResult);
            }
            catch
            {
                // Ignore tax calculation errors to avoid blocking POS operations
            }
        }

        private static string NormalizeOrderTypeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 