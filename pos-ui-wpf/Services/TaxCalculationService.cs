using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Models;

namespace POS_UI.Services
{
    /// <summary>
    /// Tax calculator for the POS. All monetary rounding is half-up to 2dp
    /// (MidpointRounding.AwayFromZero) to align with HMRC/VAT guidance.
    /// </summary>
    public class TaxCalculationService
    {
        private class ItemEvaluationState
        {
            public OrderItem Item { get; set; }
            public List<ComponentAmount> ComponentAmounts { get; set; } = new List<ComponentAmount>();
            public decimal NetTotal { get; set; }
            public bool IsHot { get; set; }
        }

        private class ComponentAmount
        {
            public OrderItemTaxComponent Component { get; set; }
            public decimal NetAmount { get; set; }
        }

        private class RuleEvaluationResult
        {
            public bool Applies { get; set; }
            public bool ContinueToNextRule { get; set; }
        }

        private class EvaluationContext
        {
            public string OrderProfileKey { get; set; }
            public bool IsRefund { get; set; }
            public bool IsHot { get; set; }
            public int? CategoryId { get; set; }
        }

        public CartTaxResult Calculate(CartService cartService, ShopModel shop, string orderType, bool isRefund = false)
        {
            var result = new CartTaxResult();
            if (cartService == null || shop == null)
            {
                return result;
            }

            if (!string.Equals(shop.CountryCode ?? string.Empty, "GB", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            var taxMode = (shop.TaxMode ?? "none").Trim();
            if (string.Equals(taxMode, "none", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }

            if (shop.TaxProfiles == null || shop.TaxProfiles.Count == 0)
            {
                return result;
            }

            var profileLookup = shop.TaxProfiles
                .GroupBy(p => p.Id)
                .Select(g => g.First())
                .ToDictionary(p => p.Id);

            var taxLookup = (shop.Taxes ?? new List<TaxModel>())
                .Where(t => t != null && t.Id > 0)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToDictionary(t => t.Id);

            if (shop.TaxProfiles != null)
            {
                foreach (var profile in shop.TaxProfiles)
                {
                    if (profile?.TaxRules == null) continue;
                    foreach (var rule in profile.TaxRules)
                    {
                        var tax = rule?.Tax;
                        if (tax == null || tax.Id <= 0) continue;
                        taxLookup[tax.Id] = tax;
                    }
                }
            }

            if (shop.TaxProfiles != null)
            {
                foreach (var profile in shop.TaxProfiles)
                {
                    if (profile?.TaxRules == null) continue;
                    foreach (var rule in profile.TaxRules)
                    {
                        var tax = rule?.Tax;
                        if (tax == null || tax.Id <= 0) continue;
                        taxLookup[tax.Id] = tax;
                    }
                }
            }

            var standardRateTax = FindStandardRateTax(shop);
            var fallbackTaxModel = standardRateTax ?? GetFallbackTaxModel(taxLookup);
            var fallbackStandardRate = fallbackTaxModel?.Rate ?? 20m;

            var orderProfileKey = NormalizeOrderProfile(orderType);
            var evaluationStates = new List<ItemEvaluationState>();
            decimal subtotalBeforeCartDiscount = 0m;

            foreach (var item in cartService.OrderItems)
            {
                OrderItemTaxComponentBuilder.EnsureComponents(item);
                var components = item.TaxComponents ?? new List<OrderItemTaxComponent>();
                if (components.Count == 0)
                {
                    components = new List<OrderItemTaxComponent>
                    {
                        new OrderItemTaxComponent
                        {
                            Key = $"fallback:{item.Id}",
                            Label = item.Product?.ItemName ?? item.Name ?? "Item",
                            UnitPrice = item.Price,
                            TaxProfileId = item.Product?.TaxProfileId
                        }
                    };
                    item.TaxComponents = components;
                }

                var state = new ItemEvaluationState
                {
                    Item = item,
                    IsHot = DetermineHeated(item)
                };

                var grossPerUnit = components.Sum(c => Math.Round(c.UnitPrice, 2, MidpointRounding.AwayFromZero));
                var grossLine = Math.Round(grossPerUnit * item.Quantity, 2, MidpointRounding.AwayFromZero);
                var netLine = Math.Round(item.Price * item.Quantity, 2, MidpointRounding.AwayFromZero);
                if (netLine < 0) netLine = 0;
                var itemLevelDiscount = Math.Max(0m, grossLine - netLine);

                decimal componentsAccumulated = 0m;
                foreach (var component in components)
                {
                    var componentGross = Math.Round(component.UnitPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
                    if (componentGross <= 0m)
                    {
                        state.ComponentAmounts.Add(new ComponentAmount { Component = component, NetAmount = 0m });
                        continue;
                    }

                    decimal componentDiscount = 0m;
                    if (itemLevelDiscount > 0m && grossLine > 0m)
                    {
                        componentDiscount = Math.Round(itemLevelDiscount * (componentGross / grossLine), 2, MidpointRounding.AwayFromZero);
                    }

                    var componentNet = Math.Max(0m, componentGross - componentDiscount);
                    state.ComponentAmounts.Add(new ComponentAmount { Component = component, NetAmount = componentNet });
                    componentsAccumulated += componentNet;
                }

                var lineDelta = netLine - componentsAccumulated;
                if (state.ComponentAmounts.Count > 0 && Math.Abs(lineDelta) >= 0.01m)
                {
                    state.ComponentAmounts[^1].NetAmount = Math.Max(0m, state.ComponentAmounts[^1].NetAmount + lineDelta);
                }

                state.NetTotal = state.ComponentAmounts.Sum(ca => ca.NetAmount);
                subtotalBeforeCartDiscount += state.NetTotal;
                evaluationStates.Add(state);
            }

            var totalDeduction = cartService.DiscountAmount + cartService.CouponAmount;
            var cartLevelDiscount = Math.Min(totalDeduction, subtotalBeforeCartDiscount);
            if (cartLevelDiscount > 0m && subtotalBeforeCartDiscount > 0m)
            {
                decimal distributed = 0m;
                for (int i = 0; i < evaluationStates.Count; i++)
                {
                    var state = evaluationStates[i];
                    if (state.NetTotal <= 0m) continue;

                    decimal itemShare = Math.Round(cartLevelDiscount * (state.NetTotal / subtotalBeforeCartDiscount), 2, MidpointRounding.AwayFromZero);
                    if (i == evaluationStates.Count - 1)
                    {
                        itemShare = cartLevelDiscount - distributed;
                    }
                    distributed += itemShare;
                    ApplyCartLevelDiscount(state, itemShare);
                }
            }

            var summaryRows = new Dictionary<string, TaxSummaryRow>(StringComparer.OrdinalIgnoreCase);
            var appliedRates = new List<decimal>();
            var appliedTaxDetails = new List<TaxDetailModel>();
            foreach (var state in evaluationStates)
            {
                var itemDetails = new List<TaxDetailModel>();
                foreach (var componentAmount in state.ComponentAmounts)
                {
                    if (componentAmount.NetAmount <= 0m) continue;
                    var componentContext = new EvaluationContext
                    {
                        OrderProfileKey = orderProfileKey,
                        IsRefund = isRefund,
                        IsHot = ResolveComponentHot(componentAmount.Component, state.IsHot),
                        CategoryId = state.Item.Product?.CategoryId
                    };
                    var componentDetails = EvaluateTaxForComponent(
                        componentAmount,
                        componentContext,
                        profileLookup,
                        taxLookup,
                        shop.TaxInclusive,
                        summaryRows,
                        appliedRates,
                        fallbackTaxModel,
                        fallbackStandardRate);
                    if (componentDetails.Count > 0)
                    {
                        itemDetails.AddRange(componentDetails);
                    }

                    var aggregatedDetail = AggregateTaxDetail(componentDetails);
                    componentAmount.Component.AppliedTaxDetail = aggregatedDetail;
                    if (componentDetails.Count > 0)
                    {
                        appliedTaxDetails.AddRange(componentDetails);
                    }
                }

                state.Item.TaxDetails = itemDetails;
                state.Item.TaxAmount = Math.Round(itemDetails.Sum(d => d.Amount), 2, MidpointRounding.AwayFromZero);
                result.ItemTaxDetails[state.Item.Id] = itemDetails;
                result.ItemTaxAmounts[state.Item.Id] = state.Item.TaxAmount;
            }

            var highestItemRate = appliedRates.Where(r => r > 0).DefaultIfEmpty(0).Max();

            if (cartService.DeliveryCharge > 0m)
            {
                // Calculate discount percentage from items to apply proportionally to delivery charge
                /*decimal deliveryChargeForTax = cartService.DeliveryCharge;
                if (cartLevelDiscount > 0m && subtotalBeforeCartDiscount > 0m)
                {
                    // Calculate the discount percentage that was applied to items
                    decimal discountPercentage = cartLevelDiscount / subtotalBeforeCartDiscount;
                    // Apply the same percentage discount to delivery charge
                    decimal deliveryDiscount = Math.Round(deliveryChargeForTax * discountPercentage, 2, MidpointRounding.AwayFromZero);
                    deliveryChargeForTax = Math.Max(0m, deliveryChargeForTax - deliveryDiscount);
                }
                */
                var deliveryRate = highestItemRate > 0m ? highestItemRate : 0m;
                var deliveryTaxModel = deliveryRate > 0m
                    ? EnsureTaxModelForRate(
                        FindTaxByRate(taxLookup, highestItemRate) ?? standardRateTax,
                        deliveryRate,
                        taxLookup,
                        standardRateTax)
                    : null;
                var deliveryTaxAmountUnrounded = deliveryRate > 0m
                    ? CalculateTaxAmount(cartService.DeliveryCharge, deliveryRate, shop.TaxInclusive)
                    : 0m;
                var deliveryReferenceDetail = GetReferenceTaxDetail(appliedTaxDetails, deliveryRate);
                result.DeliveryTaxAmount = Math.Round(deliveryTaxAmountUnrounded, 2, MidpointRounding.AwayFromZero); // Rounded for API/display
                result.DeliveryTaxId = deliveryReferenceDetail?.TaxId ?? deliveryTaxModel?.Id;
                var deliveryTaxCode = deliveryReferenceDetail?.TaxCode ??
                                      ResolveTaxCodeForRate(summaryRows, deliveryTaxModel, deliveryRate, "DELIVERY");
                result.DeliveryTaxCode = deliveryTaxCode;
                result.DeliveryTaxRate = deliveryReferenceDetail?.Rate ?? deliveryRate;
                AddSummaryRow(summaryRows, deliveryTaxCode, cartService.DeliveryCharge, deliveryTaxAmountUnrounded, deliveryRate); // Use discounted delivery charge for taxable amount
            }

            var appliedFees = cartService.GetCalculatedShopFees();
            var feeResults = new List<OrderShopFeeModel>();
            foreach (var fee in appliedFees)
            {
                if (fee == null) continue;
                var enrichedFee = new OrderShopFeeModel
                {
                    ShopFeeId = fee.ShopFeeId,
                        Type = fee.Type,
                    Name = fee.Name,
                    Amount = fee.Amount,
                    FeeType = fee.FeeType,
                        FeeValue = fee.FeeValue,
                        IsMandatory = fee.IsMandatory,
                        TaxId = fee.TaxId,
                        TaxProfileId = fee.TaxProfileId,
                        TaxCode = fee.TaxCode,
                        TaxRate = fee.TaxRate
                };

                var feeTax = new FeeTaxComputation
                {
                    ShopFeeId = fee.ShopFeeId,
                    Name = fee.Name,
                    Amount = fee.Amount
                };

                decimal targetRate = 0m;
                TaxModel targetTaxModel = null;
                var hasExplicitTax = (fee.TaxRate > 0m) || fee.TaxId.HasValue || !string.IsNullOrWhiteSpace(fee.TaxCode);

                if (hasExplicitTax)
                {
                    targetRate = fee.TaxRate;
                    targetTaxModel = EnsureTaxModelForRate(
                        FindTaxByRate(taxLookup, targetRate) ?? standardRateTax,
                        targetRate,
                        taxLookup,
                        standardRateTax);
                }
                else
                {
                    targetRate = appliedRates.Count > 0 ? appliedRates.Max() : fallbackStandardRate;
                    if (targetRate > 0m)
                    {
                        targetTaxModel = EnsureTaxModelForRate(
                            FindTaxByRate(taxLookup, targetRate) ?? standardRateTax,
                            targetRate,
                            taxLookup,
                            standardRateTax);
                    }
                    else
                    {
                        targetTaxModel = FindTaxByRate(taxLookup, 0m);
                    }
                }

                if (enrichedFee.Amount > 0m)
                {
                    var feeTaxAmountUnrounded = CalculateTaxAmount(enrichedFee.Amount, targetRate, shop.TaxInclusive);
                    feeTax.TaxAmount = Math.Round(feeTaxAmountUnrounded, 2, MidpointRounding.AwayFromZero); // Rounded for API/display
                    var feeReferenceDetail = GetReferenceTaxDetail(appliedTaxDetails, targetRate);
                    feeTax.TaxId = fee.TaxId ?? feeReferenceDetail?.TaxId ?? targetTaxModel?.Id;
                    feeTax.TaxCode = fee.TaxCode ?? feeReferenceDetail?.TaxCode ?? targetTaxModel?.Code;
                    feeTax.TaxRate = fee.TaxRate > 0 ? fee.TaxRate : feeReferenceDetail?.Rate ?? targetRate;

                    enrichedFee.TaxAmount = feeTax.TaxAmount;
                    enrichedFee.TaxId = feeTax.TaxId;
                    enrichedFee.TaxCode = feeTax.TaxCode;
                    enrichedFee.TaxRate = feeTax.TaxRate;

                    var feeTaxCode = ResolveTaxCodeForRate(summaryRows, targetTaxModel, feeTax.TaxRate, fee.Name);
                    feeTax.TaxCode = feeTaxCode;
                    AddSummaryRow(summaryRows, feeTaxCode, enrichedFee.Amount, feeTaxAmountUnrounded, feeTax.TaxRate); // Use unrounded for accurate total
                }

                result.FeeTaxes[fee.ShopFeeId] = feeTax;
                feeResults.Add(enrichedFee);
            }

            result.ShopFees = feeResults;
            
            // Round accumulated tax amounts before returning
            result.SummaryRows = summaryRows.Values
                .Select(r => new TaxSummaryRow
                {
                    TaxCode = r.TaxCode,
                    Rate = r.Rate,
                    TaxableAmount = Math.Round(r.TaxableAmount, 2, MidpointRounding.AwayFromZero),
                    TaxAmount = Math.Round(r.TaxAmount, 2, MidpointRounding.AwayFromZero)
                })
                .OrderByDescending(r => r.Rate)
                .ThenBy(r => r.TaxCode)
                .ToList();

            return result;
        }

        private static void ApplyCartLevelDiscount(ItemEvaluationState state, decimal discountAmount)
        {
            if (discountAmount <= 0m || state.NetTotal <= 0m) return;

            decimal remaining = discountAmount;
            decimal accumulated = 0m;
            for (int i = 0; i < state.ComponentAmounts.Count; i++)
            {
                var component = state.ComponentAmounts[i];
                if (component.NetAmount <= 0m) continue;

                // Guard against division by zero
                decimal componentShare = state.NetTotal > 0m 
                    ? Math.Round(discountAmount * (component.NetAmount / state.NetTotal), 2, MidpointRounding.AwayFromZero)
                    : 0m;
                if (i == state.ComponentAmounts.Count - 1)
                {
                    componentShare = Math.Min(component.NetAmount, discountAmount - accumulated);
                }
                accumulated += componentShare;
                component.NetAmount = Math.Max(0m, component.NetAmount - componentShare);
                remaining -= componentShare;
            }

            if (remaining > 0m && state.ComponentAmounts.Count > 0)
            {
                state.ComponentAmounts[^1].NetAmount = Math.Max(0m, state.ComponentAmounts[^1].NetAmount - remaining);
            }

            state.NetTotal = state.ComponentAmounts.Sum(c => c.NetAmount);
        }

        private static List<TaxDetailModel> EvaluateTaxForComponent(
            ComponentAmount componentAmount,
            EvaluationContext context,
            Dictionary<int, TaxProfileModel> profileLookup,
            Dictionary<int, TaxModel> taxLookup,
            bool taxInclusive,
            Dictionary<string, TaxSummaryRow> summaryRows,
            List<decimal> appliedRates,
            TaxModel fallbackTaxModel,
            decimal fallbackStandardRate)
        {
            var details = new List<TaxDetailModel>();
            var taxableAmount = componentAmount.NetAmount;
            if (taxableAmount <= 0m) return details;

            var taxProfileId = componentAmount.Component?.TaxProfileId;
            if (!taxProfileId.HasValue || !profileLookup.TryGetValue(taxProfileId.Value, out var profile))
            {
                AppendFallbackTaxDetail(
                    details,
                    taxableAmount,
                    taxProfileId,
                    taxLookup,
                    summaryRows,
                    appliedRates,
                    fallbackTaxModel,
                    fallbackStandardRate,
                    taxInclusive);
                return details;
            }

            if (profile.TaxRules == null || profile.TaxRules.Count == 0)
            {
                AppendFallbackTaxDetail(
                    details,
                    taxableAmount,
                    taxProfileId,
                    taxLookup,
                    summaryRows,
                    appliedRates,
                    fallbackTaxModel,
                    fallbackStandardRate,
                    taxInclusive);
                return details;
            }

            var hasAppliedRule = false;
            foreach (var rule in profile.TaxRules)
            {
                var ruleResult = EvaluateRule(rule, context);
                if (!ruleResult.Applies) continue;

                var taxModel = ResolveTaxModel(rule, taxLookup);
                var rate = taxModel?.Rate ?? 0m;
                if (rate < 0m) rate = 0m;
                var taxAmountUnrounded = CalculateTaxAmount(taxableAmount, rate, taxInclusive);

                var detail = new TaxDetailModel
                {
                    TaxProfileId = taxProfileId,
                    TaxRuleId = rule.Id,
                    TaxId = taxModel?.Id ?? rule.TaxId,
                    TaxCode = taxModel?.Code ?? $"TAX_{rule.TaxId}",
                    Rate = rate,
                    Amount = Math.Round(taxAmountUnrounded, 2, MidpointRounding.AwayFromZero), // Rounded for API/display
                    TaxableAmount = Math.Round(taxableAmount, 2, MidpointRounding.AwayFromZero)
                };
                details.Add(detail);
                AddSummaryRow(summaryRows, detail.TaxCode, taxableAmount, taxAmountUnrounded, rate); // Use unrounded for accurate total
                appliedRates.Add(rate);
                hasAppliedRule = true;

                if (!ruleResult.ContinueToNextRule)
                {
                    break;
                }
            }

            if (!hasAppliedRule)
            {
                AppendFallbackTaxDetail(
                    details,
                    taxableAmount,
                    taxProfileId,
                    taxLookup,
                    summaryRows,
                    appliedRates,
                    fallbackTaxModel,
                    fallbackStandardRate,
                    taxInclusive);
            }

            return details;
        }

        private static void AppendFallbackTaxDetail(
            List<TaxDetailModel> details,
            decimal taxableAmount,
            int? taxProfileId,
            Dictionary<int, TaxModel> taxLookup,
            Dictionary<string, TaxSummaryRow> summaryRows,
            List<decimal> appliedRates,
            TaxModel fallbackTaxModel,
            decimal fallbackStandardRate,
            bool taxInclusive)
        {
            if (taxableAmount <= 0m) return;

            var effectiveProfileId = taxProfileId ?? ResolveMaximumShopTaxProfileId();
            var effectiveModel = EnsureTaxModelForRate(
                fallbackTaxModel,
                fallbackStandardRate,
                taxLookup,
                fallbackTaxModel);
            var effectiveRate = effectiveModel?.Rate ?? fallbackStandardRate;

            if (effectiveRate > 0m)
            {
                var taxAmountUnrounded = CalculateTaxAmount(taxableAmount, effectiveRate, taxInclusive);
                var detail = new TaxDetailModel
                {
                    TaxProfileId = effectiveProfileId,
                    TaxRuleId = null,
                    TaxId = effectiveModel?.Id,
                    TaxCode = effectiveModel?.Code ?? $"TAX_{effectiveRate:0.##}",
                    Rate = effectiveRate,
                    Amount = Math.Round(taxAmountUnrounded, 2, MidpointRounding.AwayFromZero), // Rounded for API/display
                    TaxableAmount = Math.Round(taxableAmount, 2, MidpointRounding.AwayFromZero)
                };
                details.Add(detail);
                AddSummaryRow(summaryRows, detail.TaxCode, taxableAmount, taxAmountUnrounded, effectiveRate); // Use unrounded for accurate total
                appliedRates.Add(effectiveRate);
            }
            else
            {
                var zeroDetail = CreateZeroTaxDetail(taxableAmount, taxLookup, summaryRows);
                if (zeroDetail != null)
                {
                    details.Add(zeroDetail);
                    appliedRates.Add(0m);
                }
            }
        }

        private static int? ResolveMaximumShopTaxProfileId()
        {
            var profiles = GlobalDataService.Instance?.ShopDetails?.TaxProfiles;
            if (profiles == null || profiles.Count == 0) return null;

            var highest = profiles
                .Select(p => new
                {
                    Profile = p,
                    HighestRate = p?.TaxRules?.Max(r => r?.Tax?.Rate ?? 0m) ?? 0m
                })
                .OrderByDescending(x => x.HighestRate)
                .ThenByDescending(x => x.Profile?.Id ?? 0)
                .FirstOrDefault();

            return highest?.Profile?.Id ?? profiles.First().Id;
        }

        private static TaxDetailModel CreateZeroTaxDetail(decimal taxableAmount, Dictionary<int, TaxModel> taxLookup, Dictionary<string, TaxSummaryRow> summaryRows)
        {
            if (taxableAmount <= 0m) return null;
            var zeroTax = FindTaxByRate(taxLookup, 0m);
            var detail = new TaxDetailModel
            {
                TaxProfileId = null,
                TaxRuleId = null,
                TaxId = zeroTax?.Id,
                TaxCode = zeroTax?.Code ?? "ZERO",
                Rate = 0m,
                Amount = 0m,
                TaxableAmount = taxableAmount
            };
            AddSummaryRow(summaryRows, detail.TaxCode, taxableAmount, 0m, 0m);
            return detail;
        }

        private static RuleEvaluationResult EvaluateRule(TaxRuleModel rule, EvaluationContext context)
        {
            if (rule?.TaxRuleConditions == null || rule.TaxRuleConditions.Count == 0)
            {
                return new RuleEvaluationResult { Applies = true, ContinueToNextRule = false };
            }

            bool continueNext = false;
            foreach (var condition in rule.TaxRuleConditions)
            {
                if (condition == null) continue;
                var type = (condition.ConditionType ?? string.Empty).Trim().ToLowerInvariant();
                var value = condition.ConditionValue ?? string.Empty;

                switch (type)
                {
                    case "temperature":
                        if (string.Equals(value, "hot", StringComparison.OrdinalIgnoreCase) && !context.IsHot)
                            return new RuleEvaluationResult { Applies = false };
                        if (string.Equals(value, "cold", StringComparison.OrdinalIgnoreCase) && context.IsHot)
                            return new RuleEvaluationResult { Applies = false };
                        break;
                    case "order_profile":
                        if (!string.Equals(context.OrderProfileKey, value, StringComparison.OrdinalIgnoreCase))
                            return new RuleEvaluationResult { Applies = false };
                        break;
                    case "category":
                        if (!int.TryParse(value, out var categoryId) || context.CategoryId != categoryId)
                            return new RuleEvaluationResult { Applies = false };
                        break;
                    case "apply_next_matching_rule":
                        continueNext = !string.Equals(value, "0", StringComparison.OrdinalIgnoreCase);
                        break;
                    case "apply_to_refund":
                        if (context.IsRefund && string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
                            return new RuleEvaluationResult { Applies = false };
                        break;
                    default:
                        break;
                }
            }

            return new RuleEvaluationResult { Applies = true, ContinueToNextRule = continueNext };
        }

        private static void AddSummaryRow(Dictionary<string, TaxSummaryRow> summaryRows, string taxCode, decimal taxableAmount, decimal taxAmount, decimal rate)
        {
            if (string.IsNullOrWhiteSpace(taxCode))
            {
                taxCode = rate > 0 ? $"TAX_{rate:0.##}" : "TAX_0";
            }

            if (!summaryRows.TryGetValue(taxCode, out var row))
            {
                row = new TaxSummaryRow
                {
                    TaxCode = taxCode,
                    Rate = rate
                };
                summaryRows[taxCode] = row;
            }

            // Accumulate without rounding - will round when building final result
            row.TaxableAmount += taxableAmount;
            row.TaxAmount += taxAmount;
        }

        private static TaxModel ResolveTaxModel(TaxRuleModel rule, Dictionary<int, TaxModel> taxLookup)
        {
            if (rule?.Tax != null) return rule.Tax;
            if (rule != null && taxLookup.TryGetValue(rule.TaxId, out var tax))
            {
                return tax;
            }
            return null;
        }

        private static decimal CalculateTaxAmount(decimal amount, decimal rate, bool inclusive)
        {
            if (amount <= 0m || rate <= 0m) return 0m;
            if (inclusive)
            {
                // Don't round here - let AddSummaryRow handle final rounding
                return (amount * rate) / (100m + rate);
            }
            // Don't round here - let AddSummaryRow handle final rounding
            return (amount * rate) / 100m;
        }

        private static string NormalizeOrderProfile(string orderType)
        {
            if (string.IsNullOrWhiteSpace(orderType)) return "take_away";
            return orderType.Trim().ToLowerInvariant().Replace(" ", "_");
        }

        private static bool DetermineHeated(OrderItem item) =>
            OrderItemTaxComponentBuilder.IsProductHeated(item?.Product, item?.SelectedModifiers);

        private static TaxModel FindStandardRateTax(ShopModel shop)
        {
            var taxes = shop?.Taxes;
            if (taxes == null || taxes.Count == 0) return null;

            var standard = taxes.FirstOrDefault(t => Math.Abs(t.Rate - 20m) < 0.0001m);
            if (standard != null) return standard;

            return taxes.OrderByDescending(t => t.Rate).FirstOrDefault();
        }

        private static TaxModel FindTaxByRate(Dictionary<int, TaxModel> taxLookup, decimal rate)
        {
            if (taxLookup == null || taxLookup.Count == 0) return null;
            return taxLookup.Values.FirstOrDefault(t => Math.Abs(t.Rate - rate) < 0.0001m);
        }

        private static string ResolveTaxCodeForRate(Dictionary<string, TaxSummaryRow> summaryRows, TaxModel taxModel, decimal rate, string fallback)
        {
            if (summaryRows != null)
            {
                var existing = summaryRows.Values.FirstOrDefault(r => Math.Abs(r.Rate - rate) < 0.0001m && !string.IsNullOrWhiteSpace(r.TaxCode));
                if (existing != null)
                {
                    return existing.TaxCode;
                }
            }

            if (!string.IsNullOrWhiteSpace(taxModel?.Code))
            {
                return taxModel.Code;
            }

            if (!string.IsNullOrWhiteSpace(fallback))
            {
                return fallback;
            }

            if (rate <= 0m) return "ZERO";
            return $"TAX_{rate:0.##}";
        }

        private static bool IsPackagingFee(string name) =>
            string.Equals(name?.Trim(), "Packaging Fee", StringComparison.OrdinalIgnoreCase);

        private static bool IsBagFee(string name) =>
            string.Equals(name?.Trim(), "Bag Fee", StringComparison.OrdinalIgnoreCase);

        private static bool ResolveComponentHot(OrderItemTaxComponent component, bool itemIsHot)
        {
            if (component == null)
            {
                return itemIsHot;
            }

            if (!component.IsModifier && !component.IsNestedModifier)
            {
                return itemIsHot || component.IsHeated;
            }

            return component.IsHeated;
        }

        private static TaxDetailModel GetReferenceTaxDetail(List<TaxDetailModel> details, decimal rate)
        {
            if (details == null || details.Count == 0 || rate <= 0m) return null;
            return details
                .Where(d => Math.Abs(d.Rate - rate) < 0.0001m)
                .OrderByDescending(d => d.Amount)
                .FirstOrDefault();
        }

        private static TaxModel GetFallbackTaxModel(Dictionary<int, TaxModel> taxLookup)
        {
            if (taxLookup == null || taxLookup.Count == 0) return null;
            return taxLookup.Values
                .OrderByDescending(t => t.Rate)
                .FirstOrDefault();
        }

        private static TaxModel EnsureTaxModelForRate(TaxModel model, decimal rate, Dictionary<int, TaxModel> taxLookup, TaxModel standardRateTax)
        {
            if (model != null && model.Id > 0)
            {
                return model;
            }

            var resolved = FindTaxByRate(taxLookup, rate);
            if (resolved != null && resolved.Id > 0)
            {
                return resolved;
            }

            if (standardRateTax != null && Math.Abs(standardRateTax.Rate - rate) < 0.0001m)
            {
                return standardRateTax;
            }

            if (taxLookup != null && taxLookup.Count > 0)
            {
                var fallback = taxLookup.Values
                    .OrderByDescending(t => t.Rate)
                    .ThenByDescending(t => t.Id)
                    .FirstOrDefault();
                if (fallback != null)
                {
                    return fallback;
                }
            }

            return model;
        }

        private static TaxDetailModel AggregateTaxDetail(IEnumerable<TaxDetailModel> details)
        {
            if (details == null) return null;
            var list = details.Where(d => d != null).ToList();
            if (list.Count == 0) return null;

            var primary = list
                .OrderByDescending(d => d.Rate)
                .ThenByDescending(d => d.Amount)
                .FirstOrDefault();

            if (primary == null) return null;

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
    }
}

