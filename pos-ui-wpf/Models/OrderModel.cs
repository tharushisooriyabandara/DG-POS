using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using POS_UI.Services;
//using System.Windows;

namespace POS_UI.Models
{
    public enum OrderType
    {
        DineIn,
        TakeAway,
        Delivery,
        Collection
    }

    public enum OrderStatus
    {
        Draft,
        Reserved,
        Ready,
        Served,
    }

    public class OrderModel : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int ApiId { get; set; } // Integer ID for API calls
        public string OrderNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public OrderType OrderType { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Draft;
        // Raw status string from API (e.g., QUEUE, ACCEPTED, PREPARING, READY, SERVED, COMPLETED)
        public string ApiStatus { get; set; }
        public DateTime? DeliveryDateTime { get; set; }
        // Customer Information
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int? CustomerId { get; set; }

        // Delivery/Takeaway Information
        public DateTime? ScheduledTime { get; set; }
        public string DeliveryAddress { get; set; }
        public string DeliveryInstructions { get; set; }
        public int? OrderReceiverAddressId { get; set; }

        public string DeliveryCity { get; set; }
        public string DeliveryPostcode { get; set; }
        public string DeliveryAddressLine1 { get; set; }
        public string DeliveryAddressLine2 { get; set; }
        public string DeliveryFlatNo { get; set; }
        // Dine-in Information
        public int? TableNumber { get; set; }
        public string TableName { get; set; }

        // Order Session ID
        public int? OrderSessionId { get; set; }

        // Table orderings ID 
        public int TableOrderingsId { get; set; }

        // Order Items
        private List<OrderItem> _items = new List<OrderItem>();
        public List<OrderItem> Items
        {
            get => _items;
            set
            {
                _items = value;
                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(Total));
            }
        }

        // Notes 
        public string OrderNotes { get; set; }

        // Payment Information
        public decimal Total => Items?.Sum(item => item.Total) ?? 0;
        public decimal DiscountAmount { get; set; }
        public string CouponCode { get; set; }
        public decimal CouponAmount { get; set; }
        public decimal VoucherDiscount { get; set; }
        public decimal DiscountPercentage { get; set; }

        public DateTime? CreatedAtActual { get; set; }
        
        // Vouchers
        public List<VoucherModel> Vouchers { get; set; } = new List<VoucherModel>();
        public string DiscountModeApplied { get; set; } = "percentage"; // "percentage" or "value"
        public decimal DeliveryCharge { get; set; }
        public decimal RewardDiscount { get; set; }
        public decimal Subtotal => Total - DiscountAmount - CouponAmount;
        public string DiscountDescription => DiscountPercentage > 0 ? $"Discount ({DiscountPercentage}%)" : "Discount";
        public decimal? ApiTotal { get; set; }
        public decimal? ApiSubTotal { get; set; }
        public decimal DisplayTotal => (Items != null && Items.Count > 0) ? Total : (ApiTotal ?? 0);

        // Additional fee/discount components for incoming orders
        public decimal BogoDiscount { get; set; }
        public decimal TotalFee { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal ShippingTaxAmount { get; set; }
        public TaxDetailModel DeliveryTaxDetail { get; set; }

        // Payment type
        public string PaymentType { get; set; }

        // Session payment type
        public string SessionPaymentType { get; set; }

        // Payment Status
        public bool IsPaid { get; set; }
        public string PaymentMethod { get; set; }
        public DateTime? PaidAt { get; set; }

        //order type display
        public string OrderTypeDisplay
        { 
            get
            {
                var isTablePlatform = PlatformId == 8 || PlatformId2 == 8;
                if (isTablePlatform)
                {
                    var fromTable = TableOrderMethod?.Trim();
                    if (!string.IsNullOrWhiteSpace(fromTable))
                        return fromTable.ToUpperInvariant();
                }

                var ship = (ShippingMethod ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(ship))
                    return ship.ToUpperInvariant();

                return OrderType switch
                {
                    OrderType.DineIn => "DINE-IN",
                    OrderType.TakeAway => "TAKEAWAY",
                    OrderType.Delivery => "DELIVERY",
                    OrderType.Collection => "COLLECTION",
                    _ => ""
                };
            }
        }

        // Platform
        public string Platform { get; set; }
        public string PlatformName { get; set; }
        public string PlatformLogo { get; set; }
        public string PaymentStatus { get; set; }
        public string TableOrderMethod { get; set; }

        // Refund Status
        public string RefundStatus { get; set; }

        //True when platform is Table Order (8) and table_order_method is Dine-in.
        public bool IsTableOrderDineIn => (PlatformId == 8 || PlatformId2 == 8) && !string.IsNullOrWhiteSpace(TableOrderMethod) && string.Equals(TableOrderMethod.Trim(), "Dine-in", StringComparison.OrdinalIgnoreCase);

        //True when the Ready card should show "Move to Served" arrow (Dine-in or table order Dine-in)
        public bool ShowMoveToServedInReady => OrderType == OrderType.DineIn || IsTableOrderDineIn;

        public int PlatformId { get; set; } = GetPlatformIdFromShopDetails(); // Get from shop details
        public int PlatformId2 { get; set; }
        // Remote order id from delivery platform (Uber/Deliveroo/Webshop)
        public string RemoteOrderId { get; set; }

        public string DeliveryPlatfornName { get; set; }
        // True when this order came from Laravel/PHP incoming orders JSON
        public bool IsFromPhpApi { get; set; } = false;
        // Receipt Printing Properties
        public string DisplayOrderId { get; set; }

        public bool IsOtherPlatform => PlatformId != 9 && !(string.Equals(ApiStatus, "READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase) || string.Equals(ApiStatus, "SERVED", StringComparison.OrdinalIgnoreCase));
        public bool IsPaidForLiveOrder => string.Equals(PaymentStatus, "PAID", StringComparison.OrdinalIgnoreCase);
        public bool IsAlwaysCompleteAfterReady => (PlatformId == 1 || PlatformId == 2) && string.Equals(ApiStatus, "READY_FOR_PICKUP", StringComparison.OrdinalIgnoreCase);

        public string LiveOrdersStatus
        {
            get
            {
                return PlatformId == 9 ? "PREPARING" : ApiStatus;
            }
        }

        /// <summary>True when Live Orders should show table icon + label: POS dine-in, or table-order platform with dine-in method.</summary>
        public bool ShowPosDineInTableOnLiveOrders
        {
            get
            {
                var hasLabel = !string.IsNullOrWhiteSpace(TableName) || (TableNumber.HasValue && TableNumber.Value > 0);
                if (!hasLabel) return false;
                var isPosDineIn = (PlatformId == 9 || PlatformId2 == 9) && OrderType == OrderType.DineIn;
                return isPosDineIn || IsTableOrderDineIn;
            }
        }

        /// <summary>Table label for Live Orders card (name from API, or T{n} from table id when &gt; 0).</summary>
        public string LiveOrdersPosTableDisplay
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(TableName))
                    return TableName.Trim();
                if (TableNumber.HasValue && TableNumber.Value > 0)
                    return $"T{TableNumber.Value}";
                return "";
            }
        }
        public string ShippingMethod { get; set; }
        public int TableId { get; set; }
        public List<PaymentModel> Payments { get; set; } = new List<PaymentModel>();
        public List<OrderShopFeeModel> OrderShopFees { get; set; } = new List<OrderShopFeeModel>();
        public List<TaxSummaryRow> TaxSummaryRows { get; set; } = new List<TaxSummaryRow>();
        
        // Order delay status
        public bool OrderDelayed { get; set; } = false;

        // Payment mode
        public string PaymentMode { get; set; }

        // Refund balance
        public decimal RefundBalance { get; set; }

        public RefundBalancesModel? RefundBalances { get; set; }

        // Transactions (refunds, etc.)
        public List<OrderTransactionModel> Transactions { get; set; } = new List<OrderTransactionModel>();

        // User Shift ID
        public int? UserShiftId { get; set; }

        //IsTableOrder
        public bool IsTableOrder { get; set; }

        // Cash Drawer Session ID
        public int? CashDrawerSessionId { get; set; }

        // Calculate discounted delivery charge when discount or coupon is applied
        /*private decimal GetDiscountedDeliveryCharge()
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
        }*/

        // NEW: API Integration Methods
        public object ToApiRequest()
        {
            // Compute discounted delivery charge for API request
            //decimal discountedDeliveryCharge = GetDiscountedDeliveryCharge();
            
            // Compute sum of all fees to send as total_fee (shop fees + discounted delivery when applicable)
            var feesOnlyTotal = (OrderShopFees ?? new List<OrderShopFeeModel>()).Sum(f => f.Amount);
            var includeDeliveryInFees = string.Equals(ShippingMethod ?? string.Empty, "DELIVERY", StringComparison.OrdinalIgnoreCase);
            var computedTotalFee = feesOnlyTotal + (includeDeliveryInFees ? DeliveryCharge : 0m);

            // Build order items while computing an accurate items total that separates main item price from modifiers
            var orderItemPayloads = new List<object>();
            decimal itemsTotalForApi = 0m;

            if (Items != null)
            {
                foreach (var item in Items)
                {
                    if (item == null) continue;

                    var modifierUnitPrice = CalculateModifierUnitPrice(item);
                    var baseUnitPrice = ResolveBaseUnitPrice(item, modifierUnitPrice);
                    //MessageBox.Show($"baseUnitPrice: {baseUnitPrice}");
                    var unitDiscountTotal = ResolveUnitDiscountTotal(item, baseUnitPrice, modifierUnitPrice);
                    //MessageBox.Show($"unitDiscountTotal: {unitDiscountTotal}");

                    // Split discount: base portion affects price_per_item; remainder applies to modifiers
                    var baseDiscountPortion = ResolveBaseDiscountPortion(item, baseUnitPrice, modifierUnitPrice, unitDiscountTotal);
                    if (baseDiscountPortion > baseUnitPrice) baseDiscountPortion = baseUnitPrice;
                    var modifierDiscountPortion = Math.Max(0m, Math.Min(unitDiscountTotal - baseDiscountPortion, modifierUnitPrice));
                    var modifierPricePool = modifierUnitPrice;

                    var finalUnitPrice = Math.Round(Math.Max(0m, baseUnitPrice - baseDiscountPortion), 2, MidpointRounding.AwayFromZero);
                    //MessageBox.Show($"finalUnitPrice: {finalUnitPrice}");
                    var perUnitLineTotal = Math.Round(Math.Max(0m, baseUnitPrice + modifierUnitPrice - unitDiscountTotal), 2, MidpointRounding.AwayFromZero);
                    var lineTotal = Math.Round(perUnitLineTotal * item.Quantity, 2, MidpointRounding.AwayFromZero);
                    itemsTotalForApi += lineTotal;

                    orderItemPayloads.Add(new
                    {
                        // For update, prefer API item id if present; fallback to product id
                        item_id = item.ApiItemId > 0 ? item.ApiItemId : (item.Product?.Id ?? 0),
                        item_name = item.Product?.ItemName ?? item.Name,
                        quantity = item.Quantity,
                        // price_per_item should only reflect the base item (after its own discount), not modifiers
                        price_per_item = finalUnitPrice,
                        // original_price tracks the undiscounted base unit price
                        original_price = baseUnitPrice,
                        note = item.Note ?? "",
                        is_sale = false,
                        // Preserve per-unit discount for the whole unit (base + modifiers) without multiplying by quantity
                        discount_amount = unitDiscountTotal,
                        tax = Math.Round(item.TaxAmount, 2, MidpointRounding.AwayFromZero),
                        // total should include modifiers and respect per-unit discount
                        total = lineTotal,
                        tax_details = BuildTaxDetailPayload(AggregateTaxDetails(item.TaxDetails)),
                        modifier_details = (item.SelectedModifiers ?? new Dictionary<int, List<string>>())
                            .SelectMany(selectedKvp =>
                            {
                                var selectedNames = selectedKvp.Value ?? new List<string>();
                                var group = item.Product?.Modifiers?.FirstOrDefault(m => m.Id == selectedKvp.Key);

                                return selectedNames.Select(selectedName =>
                                {
                                    if (string.IsNullOrEmpty(selectedName)) return null;

                                    var modifierItem = group?.ModifierItems?.FirstOrDefault(mi => mi.ItemName == selectedName);
                                    decimal modifierItemPrice = modifierItem != null && modifierItem.OriginalPrice > 0 
                                        ? modifierItem.OriginalPrice 
                                        : (modifierItem?.ItemPrice ?? 0m);
                                    var adjustedModifierPrice = ApplyProportionalDiscount(modifierItemPrice, modifierPricePool, modifierDiscountPortion);

                                    var nestedModifiers = new List<object>();
                                    if (item.NestedModifierDetails != null &&
                                        item.NestedModifierDetails.TryGetValue(selectedName, out var nestedDetails) &&
                                        nestedDetails != null && nestedDetails.Count > 0)
                                    {
                                        foreach (var nestedDetail in nestedDetails)
                                        {
                                            int dollarIndex = nestedDetail.LastIndexOf('$');
                                            if (dollarIndex >= 0 && dollarIndex < nestedDetail.Length - 1)
                                            {
                                                string pricePart = nestedDetail.Substring(dollarIndex + 1).Trim();
                                                if (decimal.TryParse(pricePart, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                                                {
                                                    var adjustedNestedPrice = ApplyProportionalDiscount(price, modifierPricePool, modifierDiscountPortion);
                                                    var nestedInfo = nestedDetail.Substring(0, dollarIndex).Trim();
                                                    var colonIndex = nestedInfo.IndexOf(':');
                                                    if (colonIndex > 0)
                                                    {
                                                        var nestedGroupTitle = nestedInfo.Substring(0, colonIndex).Trim();
                                                        var nestedItemName = nestedInfo.Substring(colonIndex + 1).Trim();
                                                        // Try resolve against Product model when available; otherwise default ids
                                                        var resolvedNestedGroup = modifierItem?.NestedModifiers?.FirstOrDefault(nm => nm.Title == nestedGroupTitle);
                                                        var resolvedNestedItem = resolvedNestedGroup?.ModifierItems?.FirstOrDefault(nmi => nmi.ItemName == nestedItemName);
                                                        var nestedTaxDetail = FindModifierTaxDetail(item, nestedGroupTitle, nestedItemName);
                                                        var nestedModifier = new
                                                        {
                                                            modifier_main = new
                                                            {
                                                                id = resolvedNestedGroup?.Id ?? 0,
                                                                title = nestedGroupTitle,
                                                                quantity = Math.Max(resolvedNestedGroup?.DefaultQuantity ?? 1, 1)
                                                            },
                                                            modifier_item = new
                                                            {
                                                                external_item_id = resolvedNestedItem?.ExternalItemId ?? "0",
                                                                price = adjustedNestedPrice,
                                                                original_price = price,
                                                                item_name = nestedItemName,
                                                                tax_details = BuildTaxDetailPayload(nestedTaxDetail)
                                                            },
                                                            modifiers = new List<object>()
                                                        };
                                                        nestedModifiers.Add(nestedModifier);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    var modifierTaxDetail = FindModifierTaxDetail(item, group?.Title, modifierItem?.ItemName ?? selectedName);
                                    var mainModifier = new
                                    {
                                        // API expects string here (matches create flow)
                                        modifier_main = group?.Title ?? string.Empty,
                                        // Use group id for modifier_main_item to match create flow
                                            modifier_main_item = group?.Id ?? 0,
                                        quantity = Math.Max(group?.DefaultQuantity ?? 1, 1),
                                        modifier_item = modifierItem == null ? new
                                        {
                                            external_item_id = "0",
                                            price = ApplyProportionalDiscount(0.0m, modifierPricePool, modifierDiscountPortion),
                                            original_price = 0.0m,
                                            item_name = selectedName,
                                            tax_details = BuildTaxDetailPayload(modifierTaxDetail)
                                        } : new
                                        {
                                            external_item_id = modifierItem.ExternalItemId ?? "0",
                                            price = adjustedModifierPrice,
                                            original_price = modifierItemPrice,
                                            item_name = modifierItem.ItemName,
                                            tax_details = BuildTaxDetailPayload(modifierTaxDetail)
                                        },
                                        modifiers = nestedModifiers
                                    };
                                    return mainModifier;
                                });
                            })
                            .Where(x => x != null)
                            .ToList()
                    });
                }
            }

            // Compute base (items subtotal minus discounts and coupon)
            var itemsBase = itemsTotalForApi - DiscountAmount - CouponAmount;
            if (itemsBase < 0) itemsBase = 0m;

            return new
            {
                customer_id = CustomerId ?? 0,
                customer_name = CustomerName ?? string.Empty,
                delivery_date_time = (ScheduledTime ?? CreatedAt).ToString("yyyy-MM-dd HH:mm:ss"),
                platform_id = PlatformId,
                discount = DiscountAmount,
                discount_percentage = DiscountPercentage,
                discount_mode_applied = DiscountModeApplied,
                discount_type = "",
                display_order_id = DisplayOrderId,
                order_note = OrderNotes ?? string.Empty,
                total_amount = Math.Round(itemsBase + computedTotalFee, 2, MidpointRounding.AwayFromZero),
                payments = Payments.Select(p => new
                {
                    payment_method = p.PaymentMethod,
                    paying_amount = p.PayingAmount,
                    transaction_id = p.TransactionId ?? "",
                    cash = p.Cash,
                    balance = p.Balance
                }).ToList(),
                shipping_method = ShippingMethod,
                shipping_total = DeliveryCharge,
                total_fee = computedTotalFee,
                order_shop_fees = (OrderShopFees ?? new List<OrderShopFeeModel>()).Select(f => new
                {
                    shop_fee_id = f.ShopFeeId,
                    amount = f.Amount,
                    tax_id = f.TaxId ?? 0,
                    tax_code = f.TaxCode ?? string.Empty,
                    tax_rate = f.TaxRate,
                    tax_amount = Math.Round(f.TaxAmount, 2, MidpointRounding.AwayFromZero)
                }).ToList(),
                shop_id = POS_UI.Services.GlobalDataService.Instance.ShopDetails?.Id ?? 2,
                sub_total = Math.Round(itemsTotalForApi, 2, MidpointRounding.AwayFromZero),
                table_id = TableId,
                tip = 0.0,
                tip_percentage = 0.0,
                total_tax = Math.Round(TotalTaxAmount, 2, MidpointRounding.AwayFromZero),
                user_id = POS_UI.Services.GlobalDataService.Instance.CurrentUser?.Id ?? 1,
                order_receiver_address_id = OrderReceiverAddressId ?? 0,
                delivery_tax = DeliveryTaxDetail != null ? new
                {
                    tax_id = DeliveryTaxDetail.TaxId ?? 0,
                    tax_code = DeliveryTaxDetail.TaxCode ?? string.Empty,
                    tax_rate = DeliveryTaxDetail.Rate,
                    tax_amount = Math.Round(DeliveryTaxDetail.Amount, 2, MidpointRounding.AwayFromZero)
                } : null,
                order_taxes = (TaxSummaryRows ?? new List<TaxSummaryRow>()).Select(t => new
                {
                    tax_rate = t.Rate,
                    tax_code = t.TaxCode ?? string.Empty,
                    tax_amount = Math.Round(t.TaxAmount, 2, MidpointRounding.AwayFromZero),
                    taxable_amount = Math.Round(t.TaxableAmount, 2, MidpointRounding.AwayFromZero)
                }).ToList(),
                order_items = orderItemPayloads,
                vouchers = (Vouchers ?? new List<VoucherModel>()).Select(v => new
                {
                    voucher_code = v.VoucherCode ?? string.Empty,
                    voucher_value = v.VoucherValue ?? string.Empty,
                    value_type = v.ValueType ?? string.Empty,
                    voucher_discount = Math.Round(v.VoucherDiscount, 2, MidpointRounding.AwayFromZero),
                    validation = v.Validation ?? new List<object>(),
                    purchase_type = v.PurchaseType ?? string.Empty,
                    payment_type = v.PaymentType ?? string.Empty,
                    valid_categories = v.ValidCategories ?? new List<string>()
                }).ToList()
            };
        }

        // NEW: Factory method to create from CartService
        public static OrderModel FromCartService(CartService cartService, string displayOrderId, CustomerModel selectedCustomer = null, decimal discountPercentage = 0, CustomerAddressModel selectedAddress = null)
        {
            // Determine discount mode based on whether percentage or value is used
            string discountMode = discountPercentage > 0 ? "percentage" : "value";
            
            // Resolve scheduled time from cart or compute based on prep time so API gets the intended timer value
            DateTime? scheduled = cartService.SelectedOrderTime ?? cartService.PickupTime;
            if (!scheduled.HasValue)
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                var orderTypeText = cartService.OrderType ?? "Take Away";
                if ((string.Equals(orderTypeText, "Take Away", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(orderTypeText, "Delivery", StringComparison.OrdinalIgnoreCase))
                    && (shopDetails?.DeliveryPlatform?.PrepTime > 0))
                {
                    scheduled = DateTime.Now.AddMinutes(shopDetails.DeliveryPlatform.PrepTime);
                }
                else
                {
                    scheduled = DateTime.Now;
                }
            }

            // Build order_shop_fees from current shop configuration and cart amounts
            // Use CartService's calculation to respect removed optional fees and order type filtering
            var orderShopFees = cartService.GetCalculatedShopFees();

            var taxResult = cartService.CurrentTaxResult ?? new CartTaxResult();
            
            // Prefer orderShopFees derived from cartService.GetCalculatedShopFees() as it respects local filtering (removed fees, order type).
            // However, we need to populate tax info if available in taxResult.ShopFees.
            // taxResult.ShopFees likely contains ALL fees (potentially), so we should merge info into our filtered list instead of replacing it.
            
            if (orderShopFees != null && orderShopFees.Count > 0 && taxResult.ShopFees != null && taxResult.ShopFees.Count > 0)
            {
                foreach (var fee in orderShopFees)
                {
                    var taxFee = taxResult.ShopFees.FirstOrDefault(tf => tf.ShopFeeId == fee.ShopFeeId);
                    if (taxFee != null)
                    {
                        // Enforce fee amount match? Or just copy tax info? 
                        // Tax calculation should yield same fee amount if logic is consistent.
                        // Copy tax fields:
                        fee.TaxId = taxFee.TaxId;
                        fee.TaxCode = taxFee.TaxCode;
                        fee.TaxRate = taxFee.TaxRate;
                        fee.TaxAmount = taxFee.TaxAmount;
                    }
                }
            }
            // else: maintain orderShopFees as is (without tax details if taxResult is missing/empty, which is fine if no tax enabled)

            if (taxResult.ItemTaxDetails != null)
            {
                foreach (var item in cartService.OrderItems)
                {
                    if (taxResult.ItemTaxDetails.TryGetValue(item.Id, out var details))
                    {
                        item.TaxDetails = details;
                        if (taxResult.ItemTaxAmounts != null && taxResult.ItemTaxAmounts.TryGetValue(item.Id, out var taxAmount))
                        {
                            item.TaxAmount = taxAmount;
                        }
                        else
                        {
                            item.TaxAmount = details.Sum(d => d.Amount);
                        }
                    }
                }
            }

            var deliveryTaxDetail = taxResult.DeliveryTaxAmount > 0
                ? new TaxDetailModel
                {
                    TaxId = taxResult.DeliveryTaxId,
                    TaxCode = taxResult.DeliveryTaxCode,
                    Rate = taxResult.DeliveryTaxRate,
                    Amount = taxResult.DeliveryTaxAmount
                }
                : null;

            return new OrderModel
            {
                ApiId = cartService.CurrentOrderApiId ?? 0,
                DisplayOrderId = displayOrderId,
                CustomerName = selectedCustomer != null ? $"{selectedCustomer.FirstName} {selectedCustomer.LastName}" : cartService.CustomerName,
                CustomerPhone = cartService.CustomerPhone,
                CustomerId = selectedCustomer?.CustomerId ?? cartService.CurrentCustomerId,
                OrderType = ParseOrderType(cartService.OrderType),
                // Preserve API table id for dine-in updates; CartService.TableNumber mirrors API table_id when loaded
                TableNumber = cartService.TableNumber,
                DeliveryAddress = cartService.DeliveryAddress,
                OrderReceiverAddressId = selectedAddress?.Id,
                OrderNotes = cartService.Note,
                DiscountAmount = cartService.DiscountAmount,
                DiscountPercentage = discountPercentage,
                DiscountModeApplied = discountMode,
                DeliveryCharge = cartService.DeliveryCharge,
                CouponCode = cartService.CouponCode,
                CouponAmount = cartService.CouponAmount,
                Vouchers = cartService.Vouchers?.ToList() ?? new List<VoucherModel>(),
                Items = cartService.OrderItems.ToList(),
                CreatedAt = DateTime.Now,
                OrderShopFees = orderShopFees,
                TotalTaxAmount = taxResult.TotalTax,
                ShippingTaxAmount = taxResult.DeliveryTaxAmount,
                DeliveryTaxDetail = deliveryTaxDetail,
                TaxSummaryRows = taxResult.SummaryRows?.ToList() ?? new List<TaxSummaryRow>(),
                // Use resolved scheduled time for API delivery_date_time
                ScheduledTime = scheduled,
                Status = OrderStatus.Draft
            };
        }

        // NEW: Business logic methods
        public async Task<string> PlaceOrderAsync(ApiService apiService)
        {
            var request = ToApiRequest();
            return await apiService.PlaceOrderAsync(request);
        }

        public async Task<bool> UpdateOrderAsync(ApiService apiService)
        {
            var request = ToApiRequest();
            return await apiService.UpdateOrderAsync(ApiId, request);
        }

        // Helper method to parse order type
        private static OrderType ParseOrderType(string orderTypeString)
        {
            return orderTypeString?.ToLower() switch
            {
                "dine in" => OrderType.DineIn,
                "take away" => OrderType.TakeAway,
                "delivery" => OrderType.Delivery,
                "collection" => OrderType.Collection,
                _ => OrderType.TakeAway
            };
        }

        public void AddItem(OrderItem item)
        {
            Items.Add(item);
            OnPropertyChanged(nameof(Items));
            
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(Subtotal));
        }

        public void RemoveItem(OrderItem item)
        {
            Items.Remove(item);
            OnPropertyChanged(nameof(Items));
           
            OnPropertyChanged(nameof(Total));
             OnPropertyChanged(nameof(Subtotal));
        }

        private static object BuildTaxDetailPayload(TaxDetailModel detail)
        {
            if (detail == null)
            {
                return null;
            }

            return new
            {
                tax_profile_id = detail.TaxProfileId ?? 0,
                tax_rule_id = detail.TaxRuleId ?? 0,
                tax_id = detail.TaxId ?? 0,
                tax_code = detail.TaxCode ?? string.Empty,
                tax_rate = detail.Rate,
                amount = Math.Round(detail.Amount, 2, MidpointRounding.AwayFromZero)
            };
        }

        private static TaxDetailModel AggregateTaxDetails(IEnumerable<TaxDetailModel> details)
        {
            if (details == null)
            {
                return null;
            }

            var list = details.Where(d => d != null).ToList();
            if (list.Count == 0)
            {
                return null;
            }

            var primary = list
                .OrderByDescending(d => d.Rate)
                .ThenByDescending(d => d.Amount)
                .FirstOrDefault();

            if (primary == null)
            {
                return null;
            }

            return new TaxDetailModel
            {
                TaxProfileId = primary.TaxProfileId,
                TaxRuleId = primary.TaxRuleId,
                TaxId = primary.TaxId,
                TaxCode = primary.TaxCode,
                Rate = primary.Rate,
                Amount = Math.Round(list.Sum(d => d.Amount), 2, MidpointRounding.AwayFromZero),
                TaxableAmount = Math.Round(list.Sum(d => d.TaxableAmount), 2, MidpointRounding.AwayFromZero)
            };
        }

        private static TaxDetailModel FindModifierTaxDetail(OrderItem item, string groupTitle, string modifierName)
        {
            if (item?.TaxComponents == null || string.IsNullOrWhiteSpace(groupTitle) || string.IsNullOrWhiteSpace(modifierName))
            {
                return LookupExternalModifierTaxDetail(item, groupTitle, modifierName);
            }

            var label = $"{groupTitle}: {modifierName}".Trim();
            var externalDetail = LookupExternalModifierTaxDetail(item, groupTitle, modifierName);
            if (externalDetail != null)
            {
                return externalDetail;
            }

            return item.TaxComponents
                .FirstOrDefault(c => string.Equals(c.Label?.Trim(), label, StringComparison.OrdinalIgnoreCase))
                ?.AppliedTaxDetail;
        }

        private static TaxDetailModel LookupExternalModifierTaxDetail(OrderItem item, string groupTitle, string modifierName)
        {
            if (item?.ExternalModifierTaxDetails == null) return null;
            var label = $"{groupTitle}: {modifierName}".Trim();
            if (item.ExternalModifierTaxDetails.TryGetValue(label, out var detail))
            {
                return detail;
            }
            return null;
        }

        private static decimal CalculateModifierUnitPrice(OrderItem item)
        {
            if (item?.Product?.Modifiers == null || item.SelectedModifiers == null) return 0m;

            decimal total = 0m;
            foreach (var group in item.Product.Modifiers)
            {
                if (group?.ModifierItems == null) continue;
                if (!item.SelectedModifiers.TryGetValue(group.Id, out var names) || names == null || names.Count == 0) continue;

                foreach (var name in names)
                {
                    var modifierItem = group.ModifierItems.FirstOrDefault(mi => mi.ItemName == name);
                    if (modifierItem != null)
                    {
                        total += modifierItem.OriginalPrice > 0 ? modifierItem.OriginalPrice : modifierItem.ItemPrice;

                        if (modifierItem.HasNestedModifiers &&
                            item.NestedModifierDetails != null &&
                            item.NestedModifierDetails.TryGetValue(name, out var nestedList) &&
                            nestedList != null)
                        {
                            foreach (var nested in nestedList)
                            {
                                var dollarIndex = nested.LastIndexOf('$');
                                if (dollarIndex >= 0 && dollarIndex < nested.Length - 1)
                                {
                                    var pricePart = nested.Substring(dollarIndex + 1).Trim();
                                    if (decimal.TryParse(pricePart, NumberStyles.Any, CultureInfo.InvariantCulture, out var nestedPrice))
                                    {
                                        total += nestedPrice;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ResolveUnitDiscountTotal(OrderItem item, decimal baseUnitPrice, decimal modifierUnitPrice)
        {
            if (item == null) return 0m;

            decimal unitDiscount = 0m;

            // Prefer explicit discount percent when present (applies only to base price, not modifiers)
            if (item.DiscountPercent > 0m && (baseUnitPrice > 0m || modifierUnitPrice > 0m))
            {
                var discountBase = baseUnitPrice + modifierUnitPrice;
                unitDiscount = Math.Round(discountBase * item.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
            }
            else if (item.UnitDiscountAmount > 0m)
            {
                unitDiscount = Math.Round(item.UnitDiscountAmount, 2, MidpointRounding.AwayFromZero);
            }
            else if (item.DisAmount > 0m && item.Quantity > 0)
            {
                unitDiscount = Math.Round(item.DisAmount / item.Quantity, 2, MidpointRounding.AwayFromZero);
            }
            /*if (item.VisibleDiscountAmount > 0m && item.Quantity > 0)
            {
                unitDiscount = Math.Round(item.VisibleDiscountAmount / item.Quantity, 2, MidpointRounding.AwayFromZero);
            }*/

            return unitDiscount < 0m ? 0m : unitDiscount;
        }

        private static decimal ResolveBaseUnitPrice(OrderItem item, decimal modifierUnitPrice)
        {
            if (item == null) return 0m;

            decimal baseUnitPrice = item.BaseUnitPrice;
            if (baseUnitPrice <= 0m && item.Product?.PricePerItem > 0m)
            {
                baseUnitPrice = item.Product.PricePerItem;
            }
            if (baseUnitPrice <= 0m && item.Product?.Price > 0m)
            {
                baseUnitPrice = item.Product.Price;
            }

            if (baseUnitPrice <= 0m && item.Price > 0m)
            {
                // Back into the base price by removing modifiers from the unit price
                var candidate = item.Price - modifierUnitPrice;
                if (candidate > baseUnitPrice)
                {
                    baseUnitPrice = candidate;
                }
            }

            if (baseUnitPrice < 0m) baseUnitPrice = 0m;
            return Math.Round(baseUnitPrice, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal ResolveBaseDiscountPortion(OrderItem item, decimal baseUnitPrice, decimal modifierUnitPrice, decimal unitDiscountTotal)
        {
            if (item == null) return 0m;
            if (unitDiscountTotal <= 0m) return 0m;

            // If percent discount, apply percent to base price only
            if (item.DiscountPercent > 0m && baseUnitPrice > 0m)
            {
                return Math.Min(unitDiscountTotal, Math.Round(baseUnitPrice * item.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero));
            }

            // Distribute proportional to price if not a percentage discount
            var totalUnitPrice = baseUnitPrice + modifierUnitPrice;
            if (totalUnitPrice > 0m)
            {
                var baseRatio = baseUnitPrice / totalUnitPrice;
                return Math.Min(unitDiscountTotal, Math.Round(unitDiscountTotal * baseRatio, 2, MidpointRounding.AwayFromZero));
            }

            // Fallback (should not happen if totalUnitPrice > 0)
            return Math.Min(unitDiscountTotal, baseUnitPrice);
        }

        private static decimal ApplyProportionalDiscount(decimal price, decimal totalPool, decimal discountPool)
        {
            if (price <= 0m || totalPool <= 0m || discountPool <= 0m) return Math.Round(price, 2, MidpointRounding.AwayFromZero);
            var ratio = price / totalPool;
            var discounted = price - (discountPool * ratio);
            if (discounted < 0m) discounted = 0m;
            return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Helper method to get platform ID from shop details
        private static int GetPlatformIdFromShopDetails()
        {
            try
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                if (shopDetails?.DeliveryPlatform?.Id > 0)
                {
                    return shopDetails.DeliveryPlatform.Id;
                }
                
                // Fallback to default if shop details not available
                return 45;
            }
            catch
            {
                // Fallback to default if any error occurs
                return 45;
            }
        }
    }

    public class OrderShopFeeModel
    {
        public int ShopFeeId { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public string FeeType { get; set; }
        public decimal FeeValue { get; set; }
        public bool IsMandatory { get; set; }
        public int? TaxId { get; set; }
        public int? TaxProfileId { get; set; }
        public string TaxCode { get; set; }
        public decimal TaxRate { get; set; }
        public decimal TaxAmount { get; set; }
    }

    public class VoucherModel
    {
        public string VoucherCode { get; set; }
        public string VoucherValue { get; set; }
        public string ValueType { get; set; }
        public decimal VoucherDiscount { get; set; }
        public List<object> Validation { get; set; } = new List<object>();
        public string PurchaseType { get; set; }
        public string PaymentType { get; set; }
        public List<string> ValidCategories { get; set; } = new List<string>();
    }

    // Session Orders Models
    public class SessionOrdersResponse
    {
        public string Message { get; set; }
        public SessionOrdersData Data { get; set; }
        public int Code { get; set; }
    }

    public class SessionOrdersData
    {
        public string TotalAmount { get; set; }
        public string Status { get; set; }
        public string PaymentStatus { get; set; }
        public string SessionStatus { get; set; }
        public List<SessionOrderDetail> OrderDetails { get; set; } = new List<SessionOrderDetail>();
    }

    public class SessionOrderDetail
    {
        public string DisplayOrderId { get; set; }
        public string Status { get; set; }
        public int OrderApiId { get; set; }
        /// <summary>Order total amount.</summary>
        public decimal TotalAmount { get; set; }
        /// <summary>Table ID for this order in the session.</summary>
        public int TableId { get; set; }

        /// <summary>Optional fields from session-orders API when present.</summary>
        public string PlatformName { get; set; }
        public string PlatformLogo { get; set; }
        public string PaymentStatus { get; set; }
        public string ShippingMethod { get; set; }
        public string TableOrderMethod { get; set; }
        public string ApiStatus { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? DeliveryDateTime { get; set; }
        public string CustomerName { get; set; }
    }

    public class OrderTransactionModel
    {
        public string TransactionType { get; set; }
        public decimal TransactionAmount { get; set; }
        public string TransactionMode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Reason { get; set; }
    }
} 