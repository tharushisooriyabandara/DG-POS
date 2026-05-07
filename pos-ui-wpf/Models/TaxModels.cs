using System;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public class TaxModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public string Description { get; set; }
        public decimal Rate { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TaxRuleConditionModel
    {
        public int Id { get; set; }
        public int TaxRuleId { get; set; }
        public string ConditionType { get; set; }
        public string ConditionValue { get; set; }
        public decimal MinValue { get; set; }
        public decimal MaxValue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class TaxRuleModel
    {
        public int Id { get; set; }
        public int TaxProfileId { get; set; }
        public int TaxId { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public TaxModel Tax { get; set; }
        public List<TaxRuleConditionModel> TaxRuleConditions { get; set; } = new List<TaxRuleConditionModel>();
    }

    public class TaxProfileModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<TaxRuleModel> TaxRules { get; set; } = new List<TaxRuleModel>();
    }

    public class TaxSummaryRow
    {
        public string TaxCode { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal Rate { get; set; }
    }

    public class TaxDetailModel
    {
        public int? TaxProfileId { get; set; }
        public int? TaxRuleId { get; set; }
        public int? TaxId { get; set; }
        public decimal Amount { get; set; }
        public string TaxCode { get; set; }
        public decimal Rate { get; set; }
        public decimal TaxableAmount { get; set; }
        public bool IsComponentDetail { get; set; }
    }

    public class OrderItemTaxComponent
    {
        public string Key { get; set; }
        public string Label { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Quantity { get; set; } = 1m;
        public decimal TotalPrice => Math.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
        public int? TaxProfileId { get; set; }
        public bool IsModifier { get; set; }
        public bool IsNestedModifier { get; set; }
        public bool IsHeated { get; set; }
        public TaxDetailModel AppliedTaxDetail { get; set; }

        public OrderItemTaxComponent Clone()
        {
            return new OrderItemTaxComponent
            {
                Key = Key,
                Label = Label,
                UnitPrice = UnitPrice,
                Quantity = Quantity,
                TaxProfileId = TaxProfileId,
                IsModifier = IsModifier,
                IsNestedModifier = IsNestedModifier,
                IsHeated = IsHeated,
                AppliedTaxDetail = AppliedTaxDetail
            };
        }
    }
}

