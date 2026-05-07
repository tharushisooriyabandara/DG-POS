using System;
using System.Collections.Generic;
using System.Linq;

namespace POS_UI.Models
{
    public class ZReportStatsModel
    {
        public Dictionary<string, PlatformStatsModel> PlatformStats { get; set; } = new Dictionary<string, PlatformStatsModel>();
        public string From { get; set; }
        public string To { get; set; }
        public string TotalNetSales { get; set; }
        public string TotalCancellations { get; set; }
        public string TotalMissed { get; set; }
        public string TotalRefunds { get; set; }
        public string TotalDiscount { get; set; }
        public string TotalGrossSales { get; set; }
        public string RestaurantName { get; set; }
        public TaxSummaryModel TaxSummary { get; set; }

        public int TakeawayOrderCount { get; private set; }
        public int DineInOrderCount { get; private set; }
        public int PosDeliveryOrderCount { get; private set; }
        public int CollectionOrderCount { get; private set; }
        public int OnlineDeliveryOrderCount { get; private set; }
        public int PlatformOrderCount { get; private set; }
        public int TotalOrderCount { get; private set; }
        public int PosCashOrderCount { get; private set; }
        public int PlatformCashOrderCount { get; private set; }
        public int CardMachineOrderCount { get; private set; }
        public int TotalTenderOrderCount { get; private set; }
        public int PosDiscountOrderCount { get; private set; }
        public int PlatformDiscountOrderCount { get; private set; }
        public int TotalDiscountOrderCount { get; private set; }

        public decimal TakeawayRevenue { get; private set; }
        public decimal DineInRevenue { get; private set; }
        public decimal PosDeliveryRevenue { get; private set; }
        public decimal CollectionRevenue { get; private set; }
        public decimal OnlineDeliveryRevenue { get; private set; }
        public decimal PlatformRevenue { get; private set; }
        public decimal TotalRevenue { get; private set; }
        public decimal PosCashSales { get; private set; }
        public decimal PlatformCashSales { get; private set; }
        public decimal CardMachineSales { get; private set; }
        public decimal TotalTenderSales { get; private set; }
        public decimal PosCashRefund { get; private set; }
        public decimal PosCardRefund { get; private set; }
        public decimal PosCashSaleCashRefund { get; private set; }
        public decimal PosCardSaleCashRefund { get; private set; }
        public decimal OnlineGatewayRefunds { get; private set; }
        public decimal TotalRefundsCalculated { get; private set; }
        public decimal PosVoidSales { get; private set; }
        public decimal PlatformVoidSales { get; private set; }
        public decimal TotalVoidSales { get; private set; }
        public decimal PosDiscount { get; private set; }
        public decimal PlatformDiscount { get; private set; }
        public decimal PosVoucherDiscount { get; private set; }
        public decimal PlatformVoucherDiscount { get; private set; }
        public decimal TotalDiscountCalculated { get; private set; }
        


        /// <summary>
        /// Helper method to parse formatted currency string to decimal
        /// </summary>
        private decimal ParseCurrencyString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0m;

            // Remove commas and spaces, then parse
            var cleaned = value.Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            return 0m;
        }

        /// <summary>
        /// Calculates and sets all order count and revenue properties based on PlatformStats
        /// OPTIMIZED: Single-pass calculation instead of 20+ loops
        /// </summary>
        public void CalculateOrderCounts()
        {
            // Reset all values
            TakeawayOrderCount = 0;
            TakeawayRevenue = 0m;
            DineInOrderCount = 0;
            DineInRevenue = 0m;
            PosDeliveryOrderCount = 0;
            PosDeliveryRevenue = 0m;
            PosCashOrderCount = 0;
            PlatformCashOrderCount = 0;
            PosCashSales = 0m;
            PlatformCashSales = 0m;
            CardMachineOrderCount = 0;
            CardMachineSales = 0m;
            CollectionOrderCount = 0;
            CollectionRevenue = 0m;
            OnlineDeliveryOrderCount = 0;
            OnlineDeliveryRevenue = 0m;
            PosCashRefund = 0m;
            PosCardRefund = 0m;
            PosCashSaleCashRefund = 0m;
            PosCardSaleCashRefund = 0m;
            OnlineGatewayRefunds = 0m;
            PosVoidSales = 0m;
            PlatformVoidSales = 0m;
            PosDiscountOrderCount = 0;
            PlatformDiscountOrderCount = 0;
            PosDiscount = 0m;
            PlatformDiscount = 0m;
            PosVoucherDiscount = 0m;
            PlatformVoucherDiscount = 0m;
            
            decimal cardOnlineRevenue = 0m;
            int cardOnlineOrderCount = 0;
            
            // OPTIMIZATION: Single pass through all platforms
            var platformCashOrderIds = new[] { 1, 2, 6, 7, 8 };
            
            foreach (var platform in PlatformStats.Values)
            {
                bool isPOS = platform.PlatformId == 9;
                bool isWebshop = platformCashOrderIds.Contains(platform.PlatformId);
                
                // Process POS platforms (PlatformId == 9)
                if (isPOS)
                {
                    // Gross Sales
                    if (platform.GrossSales.TakeawayOrders.HasValue)
                        TakeawayOrderCount += platform.GrossSales.TakeawayOrders.Value;
                    if (!string.IsNullOrWhiteSpace(platform.GrossSales.TakeawayOrderRevenue))
                        TakeawayRevenue += ParseCurrencyString(platform.GrossSales.TakeawayOrderRevenue);
                    
                    if (platform.GrossSales.DineInOrders.HasValue)
                        DineInOrderCount += platform.GrossSales.DineInOrders.Value;
                    if (!string.IsNullOrWhiteSpace(platform.GrossSales.DineInOrderRevenue))
                        DineInRevenue += ParseCurrencyString(platform.GrossSales.DineInOrderRevenue);
                    
                    if (platform.GrossSales.DeliveryOrders.HasValue)
                        PosDeliveryOrderCount += platform.GrossSales.DeliveryOrders.Value;
                    if (!string.IsNullOrWhiteSpace(platform.GrossSales.DeliveryOrderRevenue))
                        PosDeliveryRevenue += ParseCurrencyString(platform.GrossSales.DeliveryOrderRevenue);
                    
                    // Tender Summary
                    PosCashOrderCount += platform.TenderSummary.CashOrderCount;
                    if (!string.IsNullOrWhiteSpace(platform.TenderSummary.CashRevenue))
                        PosCashSales += ParseCurrencyString(platform.TenderSummary.CashRevenue);
                    
                    if (platform.TenderSummary.CardOrderCount.HasValue)
                        CardMachineOrderCount += platform.TenderSummary.CardOrderCount.Value;
                    if (!string.IsNullOrWhiteSpace(platform.TenderSummary.CardRevenue))
                        CardMachineSales += ParseCurrencyString(platform.TenderSummary.CardRevenue);
                    
                    // Refunds
                    if (!string.IsNullOrWhiteSpace(platform.RefundSummary.CashRefund))
                        PosCashRefund += ParseCurrencyString(platform.RefundSummary.CashRefund);
                    if (!string.IsNullOrWhiteSpace(platform.RefundSummary.CardRefund))
                        PosCardRefund += ParseCurrencyString(platform.RefundSummary.CardRefund);
                    if (!string.IsNullOrWhiteSpace(platform.RefundSummary?.CashSaleCashRefund))
                        PosCashSaleCashRefund += ParseCurrencyString(platform.RefundSummary.CashSaleCashRefund);
                    if (!string.IsNullOrWhiteSpace(platform.RefundSummary?.CardSaleCashRefund))
                        PosCardSaleCashRefund += ParseCurrencyString(platform.RefundSummary.CardSaleCashRefund);
                    
                    // Voids
                    if (!string.IsNullOrWhiteSpace(platform.UnfulfilledSummary.Voids))
                        PosVoidSales += ParseCurrencyString(platform.UnfulfilledSummary.Voids);
                    
                    // Discounts
                    PosDiscountOrderCount += platform.DiscountSummary.DiscountOrderCount;
                    if (!string.IsNullOrWhiteSpace(platform.DiscountSummary.Discount))
                        PosDiscount += ParseCurrencyString(platform.DiscountSummary.Discount);
                    if (!string.IsNullOrWhiteSpace(platform.DiscountSummary.VoucherDiscount))
                        PosVoucherDiscount += ParseCurrencyString(platform.DiscountSummary.VoucherDiscount);
                }
                
                // Process Webshop/Platform orders (PlatformId in [1, 2, 6, 7, 8])
                if (isWebshop)
                {
                    // Gross Sales
                    if (platform.GrossSales.CollectionOrders.HasValue)
                        CollectionOrderCount += platform.GrossSales.CollectionOrders.Value;
                    if (!string.IsNullOrWhiteSpace(platform.GrossSales.CollectionRevenue))
                        CollectionRevenue += ParseCurrencyString(platform.GrossSales.CollectionRevenue);
                    
                    if (platform.GrossSales.DeliveryOrders.HasValue)
                        OnlineDeliveryOrderCount += platform.GrossSales.DeliveryOrders.Value;
                    if (!string.IsNullOrWhiteSpace(platform.GrossSales.DeliveryOrderRevenue))
                        OnlineDeliveryRevenue += ParseCurrencyString(platform.GrossSales.DeliveryOrderRevenue);
                    
                    // Tender Summary
                    PlatformCashOrderCount += platform.TenderSummary.CashOrderCount;
                    if (!string.IsNullOrWhiteSpace(platform.TenderSummary.CashRevenue))
                        PlatformCashSales += ParseCurrencyString(platform.TenderSummary.CashRevenue);
                    
                    if (platform.TenderSummary.CardMachineOrderCount.HasValue)
                        CardMachineOrderCount += platform.TenderSummary.CardMachineOrderCount.Value;
                    if (!string.IsNullOrWhiteSpace(platform.TenderSummary.CardMachineRevenue))
                        CardMachineSales += ParseCurrencyString(platform.TenderSummary.CardMachineRevenue);
                    
                    if (platform.TenderSummary.CardOnlineOrderCount.HasValue)
                        cardOnlineOrderCount += platform.TenderSummary.CardOnlineOrderCount.Value;
                    if (!string.IsNullOrWhiteSpace(platform.TenderSummary.CardOnlineRevenue))
                        cardOnlineRevenue += ParseCurrencyString(platform.TenderSummary.CardOnlineRevenue);
                    
                    // Refunds
                    if (!string.IsNullOrWhiteSpace(platform.RefundSummary.CardOnlineRefund))
                        OnlineGatewayRefunds += ParseCurrencyString(platform.RefundSummary.CardOnlineRefund);
                    
                    // Voids
                    if (!string.IsNullOrWhiteSpace(platform.UnfulfilledSummary.Voids))
                        PlatformVoidSales += ParseCurrencyString(platform.UnfulfilledSummary.Voids);
                    
                    // Discounts
                    PlatformDiscountOrderCount += platform.DiscountSummary.DiscountOrderCount;
                    if (!string.IsNullOrWhiteSpace(platform.DiscountSummary.Discount))
                        PlatformDiscount += ParseCurrencyString(platform.DiscountSummary.Discount);
                    if (!string.IsNullOrWhiteSpace(platform.DiscountSummary.VoucherDiscount))
                        PlatformVoucherDiscount += ParseCurrencyString(platform.DiscountSummary.VoucherDiscount);
                }
            }
            
            // Calculate derived totals after the single loop
            // Calculate derived totals
            PlatformOrderCount = CollectionOrderCount + OnlineDeliveryOrderCount;
            PlatformRevenue = CollectionRevenue + OnlineDeliveryRevenue;
            TotalOrderCount = TakeawayOrderCount + DineInOrderCount + PosDeliveryOrderCount + PlatformOrderCount;
            TotalRevenue = TakeawayRevenue + DineInRevenue + PosDeliveryRevenue + PlatformRevenue;
            TotalTenderSales = PosCashSales + PlatformCashSales + CardMachineSales + cardOnlineRevenue;
            TotalTenderOrderCount = PosCashOrderCount + PlatformCashOrderCount + CardMachineOrderCount + cardOnlineOrderCount;
            TotalRefundsCalculated = PosCashRefund + PosCardRefund;
            TotalVoidSales = PosVoidSales + PlatformVoidSales;
            TotalDiscountOrderCount = PosDiscountOrderCount + PlatformDiscountOrderCount;
            TotalDiscountCalculated = PosDiscount + PlatformDiscount + PosVoucherDiscount + PlatformVoucherDiscount;
        }
    }

    public class PlatformStatsModel
    {
        public int PlatformId { get; set; }
        public GrossSalesModel GrossSales { get; set; } = new GrossSalesModel();
        public TenderSummaryModel TenderSummary { get; set; } = new TenderSummaryModel();
        public RefundSummaryModel RefundSummary { get; set; } = new RefundSummaryModel();
        public UnfulfilledSummaryModel UnfulfilledSummary { get; set; } = new UnfulfilledSummaryModel();
        public DiscountSummaryModel DiscountSummary { get; set; } = new DiscountSummaryModel();
    }

    public class GrossSalesModel
    {
        // For Webshop, Table Order platforms
        public int? CollectionOrders { get; set; }
        public string CollectionRevenue { get; set; }
        public int? DeliveryOrders { get; set; }
        public string DeliveryOrderRevenue { get; set; }

        // For DG POS platform
        public int? TakeawayOrders { get; set; }
        public string TakeawayOrderRevenue { get; set; }
        public int? DineInOrders { get; set; }
        public string DineInOrderRevenue { get; set; }
    }

    public class TenderSummaryModel
    {
        public int CashOrderCount { get; set; }
        public string CashRevenue { get; set; }
        
        // For Webshop, Table Order platforms
        public int? CardOnlineOrderCount { get; set; }
        public string CardOnlineRevenue { get; set; }
        public int? CardMachineOrderCount { get; set; }
        public string CardMachineRevenue { get; set; }

        // For DG POS platform
        public int? CardOrderCount { get; set; }
        public string CardRevenue { get; set; }
    }

    public class RefundSummaryModel
    {
        public string CashRefund { get; set; }
        public string CardRefund { get; set; }
        public string CardOnlineRefund { get; set; }
        public string TotalRefund { get; set; }
        public string CashSaleCashRefund { get; set; }
        public string CardSaleCashRefund { get; set; }
    }

    public class UnfulfilledSummaryModel
    {
        public string Cancellations { get; set; }
        public string Missed { get; set; }
        public string Voids { get; set; }
    }

    public class DiscountSummaryModel
    {
        public string Discount { get; set; }
        public int DiscountOrderCount { get; set; }
        public string VoucherDiscount { get; set; }

    }
}

