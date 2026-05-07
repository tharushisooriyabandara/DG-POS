using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public class ShopModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string ShopLogo { get; set; }
        public int FranchiseId { get; set; }
        public string Code { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string ContactNo { get; set; }
        public string BusinessRegNo { get; set; }
        public string TaxRegNo { get; set; }
        public string Status { get; set; }
        public string OrderStatus { get; set; }
        public int LastUpdatedMenu { get; set; }
        public string ServiceAvailability { get; set; }
        public decimal MinimumAmountForFreeDelivery { get; set; }
        public decimal MinimumAmountForDelivery { get; set; }
        public decimal OneTimePromotionValue { get; set; }
        public decimal OneTimePromotionSpendAmount { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string GoogleLocationUrl { get; set; }
        public string OneTimePromotionType { get; set; }
        public decimal MaximumPromotionValue { get; set; }
        public int SelectedMenu { get; set; }
        public bool IsDefault { get; set; }
        public string CountryCode { get; set; }
        public string Timezone { get; set; }
        public string Currency { get; set; }
        public string CurrencyCode { get; set; }
        public bool TaxInclusive { get; set; }
        public string TaxMode { get; set; } = "none";
        public bool DeliveryPlatformEnable { get; set; }
        public bool HasCashPayment { get; set; }
        public bool HasCardPayment { get; set; }
        public bool DelivergateAccount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DeliveryPlatformModel DeliveryPlatform { get; set; }
        public List<ShopFeeModel> ShopFees { get; set; }
        public List<TaxProfileModel> TaxProfiles { get; set; } = new List<TaxProfileModel>();
        public List<TaxModel> Taxes { get; set; } = new List<TaxModel>();
        public List<PrinterGroupModel> PrinterGroups { get; set; } = new List<PrinterGroupModel>();

        public bool HasUkTaxProfiles =>
            string.Equals(CountryCode ?? string.Empty, "GB", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(TaxMode ?? "none", "none", StringComparison.OrdinalIgnoreCase) &&
            TaxProfiles != null &&
            TaxProfiles.Count > 0;
    }

    public class DeliveryPlatformModel
    {
        public int Id { get; set; }
        public int PlatformId { get; set; }
        public string Name { get; set; }
        public string Logo { get; set; }
        public string Status { get; set; }
        public string OutletCode { get; set; }
        public int FranchiseId { get; set; }
        public int OutletId { get; set; }
        public string StoreStatus { get; set; }
        public DateTime AvailableFrom { get; set; }
        public bool IsMaster { get; set; }
        public int ParentPlatform { get; set; }
        public int PrepTime { get; set; }
        public bool OwnDriver { get; set; }
        public DateTime MenuPublishedAt { get; set; }
        public bool WebshopSetupStatus { get; set; }
        public bool HasCashPayment { get; set; }
        public bool HasCardPayment { get; set; }
        public int SelectedMenu { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string BrandName { get; set; }
    }

    public class ShopFeeModel
    {
        public int Id { get; set; } // optional if API provides
        public string Type { get; set; } // TAKEAWAY / DINE-IN / DELIVERY
        public string FeeType { get; set; } // "PERCENTAGE" or "VALUE"
        public string FeeName { get; set; }
        public decimal Fee { get; set; }
        public bool Mandatory { get; set; }

        // Optional tax details supplied per shop fee
        public int? TaxId { get; set; }
        public int? TaxProfileId { get; set; }
        public string TaxCode { get; set; }
        public decimal TaxRate { get; set; }
    }

    public class PrinterGroupModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
} 