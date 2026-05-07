using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Models;

namespace POS_UI.Services
{
    public static class OrderItemTaxComponentBuilder
    {
        public static List<OrderItemTaxComponent> Build(ProductItemModel product, decimal baseUnitPrice, Dictionary<int, List<string>> selectedModifiers, Dictionary<string, List<string>> nestedModifierDetails)
        {
            var components = new List<OrderItemTaxComponent>();
            var normalizedBasePrice = Math.Round(baseUnitPrice, 2, MidpointRounding.AwayFromZero);
            if (normalizedBasePrice <= 0 && product != null)
            {
                normalizedBasePrice = Math.Round(product.PricePerItem > 0 ? product.PricePerItem : product.Price, 2, MidpointRounding.AwayFromZero);
            }
            if (normalizedBasePrice < 0) normalizedBasePrice = 0;

            var baseLabel = product?.ItemName ?? "Item";
            var productHeated = IsProductHeated(product, selectedModifiers);
            var baseTaxProfileId = product?.TaxProfileId ?? ResolveFallbackProfileId(product);
            var baseComponent = new OrderItemTaxComponent
            {
                Key = $"base:{product?.Id ?? 0}",
                Label = baseLabel,
                UnitPrice = normalizedBasePrice,
                TaxProfileId = baseTaxProfileId,
                IsModifier = false,
                IsHeated = productHeated
            };
            components.Add(baseComponent);

            if (selectedModifiers != null && product?.Modifiers != null)
            {
                foreach (var kvp in selectedModifiers)
                {
                    var group = product.Modifiers.FirstOrDefault(g => g.Id == kvp.Key);
                    if (group == null) continue;
                    foreach (var selectedName in kvp.Value ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(selectedName)) continue;
                        var modifierItem = group.ModifierItems?.FirstOrDefault(m => string.Equals(m.ItemName, selectedName, StringComparison.OrdinalIgnoreCase));
                        var modifierPrice = modifierItem?.ItemPrice ?? 0m;
                        var inheritsBaseTax = group?.IsTaxInherited == true;
                        var modifierHeated = inheritsBaseTax ? productHeated : IsModifierHot(group, selectedName);
                        var modifierTaxProfileId = ResolveModifierTaxProfileId(
                            product,
                            group,
                            modifierItem,
                            inheritsBaseTax ? baseComponent?.TaxProfileId : null);
                        var modifierComponent = new OrderItemTaxComponent
                        {
                            Key = $"mod:{group.Id}:{modifierItem?.Id ?? 0}:{selectedName}",
                            Label = $"{group.Title}: {selectedName}",
                            UnitPrice = Math.Round(modifierPrice, 2, MidpointRounding.AwayFromZero),
                            TaxProfileId = modifierTaxProfileId,
                            IsModifier = true,
                            IsHeated = modifierHeated
                        };
                        components.Add(modifierComponent);

                        if (nestedModifierDetails != null && nestedModifierDetails.TryGetValue(selectedName, out var nestedList))
                        {
                            var nestedMakesHot = AppendNestedComponents(components, modifierComponent, product, modifierItem, nestedList, productHeated);
                            if (nestedMakesHot)
                            {
                                modifierComponent.IsHeated = true;
                            }
                        }
                    }
                }
            }
            else if (nestedModifierDetails != null && nestedModifierDetails.Count > 0)
            {
                // Nested modifiers present without explicit parent selection (fallback)
                foreach (var entry in nestedModifierDetails)
                {
                    AppendNestedComponents(components, null, product, FindModifierItemByName(product, entry.Key), entry.Value, productHeated);
                }
            }

            return components.Where(c => c != null && c.UnitPrice >= 0).ToList();
        }

        public static void EnsureComponents(OrderItem item)
        {
            if (item == null) return;
            if (item.TaxComponents != null && item.TaxComponents.Count > 0) return;
            var basePrice = item.BaseUnitPrice > 0
                ? item.BaseUnitPrice
                : (item.Product?.PricePerItem > 0 ? item.Product.PricePerItem
                    : item.Product?.Price > 0 ? item.Product.Price
                    : item.Price);

            var selectedModifiers = item.SelectedModifiers ?? new Dictionary<int, List<string>>();
            var nestedDetails = item.NestedModifierDetails ?? new Dictionary<string, List<string>>();
            item.TaxComponents = Build(item.Product, basePrice, selectedModifiers, nestedDetails);

            if (item.TaxComponents == null || item.TaxComponents.Count == 0)
            {
                // Fallback single component covering full unit price
                item.TaxComponents = new List<OrderItemTaxComponent>
                {
                    new OrderItemTaxComponent
                    {
                        Key = $"fallback:{item.Id}",
                        Label = item.Product?.ItemName ?? item.Name ?? "Item",
                        UnitPrice = item.Price,
                        TaxProfileId = item.Product?.TaxProfileId,
                        IsHeated = IsProductHeated(item.Product, selectedModifiers)
                    }
                };
            }
        }

        private static bool AppendNestedComponents(List<OrderItemTaxComponent> components, OrderItemTaxComponent parentComponent, ProductItemModel product, ModifierItemModel parentModifier, List<string> nestedDetails, bool itemIsHot)
        {
            if (components == null || nestedDetails == null) return false;
            var parentHeated = false;
            foreach (var detail in nestedDetails)
            {
                if (string.IsNullOrWhiteSpace(detail)) continue;
                var parsed = ParseNestedDetail(detail);
                var nestedGroup = parentModifier?.NestedModifiers?.FirstOrDefault(g => string.Equals(g.Title?.Trim(), parsed.GroupTitle, StringComparison.OrdinalIgnoreCase));
                var nestedItem = nestedGroup?.ModifierItems?.FirstOrDefault(i => string.Equals(i.ItemName?.Trim(), parsed.ItemName, StringComparison.OrdinalIgnoreCase));
                var label = $"{parsed.GroupTitle}: {parsed.ItemName}";
                var unitPrice = nestedItem?.ItemPrice ?? parsed.Price ?? 0m;
                var isHeatToggle = IsHeatedGroupSelection(parsed.GroupTitle, parsed.ItemName);
                if (isHeatToggle)
                {
                    parentHeated = true;
                    continue;
                }
                var inheritsBaseTax = nestedGroup?.IsTaxInherited == true;
                var nestedIsHot = inheritsBaseTax
                    ? (parentComponent?.IsHeated ?? itemIsHot)
                    : IsNestedModifierHot(parsed.GroupTitle, parsed.ItemName);
                var inheritedProfileId = inheritsBaseTax
                    ? parentComponent?.TaxProfileId ?? parentModifier?.TaxProfileId
                    : null;
                var nestedTaxProfileId = ResolveModifierTaxProfileId(product, nestedGroup, nestedItem, inheritedProfileId);
                components.Add(new OrderItemTaxComponent
                {
                    Key = $"nested:{parentModifier?.Id ?? 0}:{nestedGroup?.Id ?? 0}:{nestedItem?.Id ?? 0}:{parsed.ItemName}",
                    Label = label,
                    UnitPrice = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero),
                    TaxProfileId = nestedTaxProfileId,
                    IsModifier = true,
                    IsNestedModifier = true,
                    IsHeated = nestedIsHot
                });
            }
            if (parentComponent != null && parentHeated)
            {
                parentComponent.IsHeated = true;
            }
            return parentHeated;
        }

        private static (string GroupTitle, string ItemName, decimal? Price) ParseNestedDetail(string detail)
        {
            var text = detail?.Trim() ?? string.Empty;
            decimal? price = null;
            var dollarIndex = text.LastIndexOf('$');
            if (dollarIndex > 0 && dollarIndex < text.Length - 1)
            {
                var pricePart = text.Substring(dollarIndex + 1).Trim();
                if (decimal.TryParse(pricePart, out var parsedPrice))
                {
                    price = parsedPrice;
                    text = text.Substring(0, dollarIndex).Trim();
                }
            }

            var colonIndex = text.IndexOf(':');
            if (colonIndex <= 0)
            {
                return (text, text, price);
            }

            var groupTitle = text.Substring(0, colonIndex).Trim();
            var itemName = text.Substring(colonIndex + 1).Trim();
            return (groupTitle, itemName, price);
        }

        private static ModifierItemModel FindModifierItemByName(ProductItemModel product, string itemName)
        {
            if (product?.Modifiers == null || string.IsNullOrWhiteSpace(itemName)) return null;
            foreach (var group in product.Modifiers)
            {
                var match = group.ModifierItems?.FirstOrDefault(mi => string.Equals(mi.ItemName, itemName, StringComparison.OrdinalIgnoreCase));
                if (match != null) return match;
            }
            return null;
        }

        public static bool IsProductHeated(ProductItemModel product, Dictionary<int, List<string>> selectedModifiers)
        {
            if (product?.Modifiers == null || selectedModifiers == null) return false;
            foreach (var group in product.Modifiers)
            {
                if (!string.Equals(group?.Title ?? string.Empty, "Heated", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (selectedModifiers.TryGetValue(group.Id, out var selections) && selections != null)
                {
                    if (selections.Any(selection => IsHeatedSelection(selection)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsModifierHot(ModifierModel group, string modifierName)
        {
            if (group == null || string.IsNullOrWhiteSpace(modifierName)) return false;
            var title = group.Title ?? string.Empty;
            var normalized = modifierName.Trim();

            if (string.Equals(title, "Heated", StringComparison.OrdinalIgnoreCase))
            {
                return IsHeatedSelection(normalized);
            }

            if (string.Equals(title, "Temperature", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(normalized, "Hot", StringComparison.OrdinalIgnoreCase);
            }

            return ContainsHotKeyword(normalized);
        }

        private static bool IsNestedModifierHot(string groupTitle, string modifierName)
        {
            if (!string.IsNullOrWhiteSpace(groupTitle) &&
                string.Equals(groupTitle.Trim(), "Temperature", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(modifierName?.Trim(), "Hot", StringComparison.OrdinalIgnoreCase);
            }

            return ContainsHotKeyword(modifierName);
        }

        private static bool IsHeatedGroupSelection(string groupTitle, string modifierName)
        {
            var normalizedGroup = groupTitle?.Trim() ?? string.Empty;
            var normalizedModifier = modifierName?.Trim() ?? string.Empty;

            if (string.Equals(normalizedGroup, "Heated", StringComparison.OrdinalIgnoreCase))
            {
                return IsHeatedSelection(normalizedModifier);
            }

            if (string.Equals(normalizedGroup, "Temperature", StringComparison.OrdinalIgnoreCase))
            {
                return IsHeatedSelection(normalizedModifier);
            }

            return false;
        }

        private static bool IsHeatedSelection(string selection)
        {
            if (string.IsNullOrWhiteSpace(selection)) return false;
            var normalized = selection.Trim();
            return string.Equals(normalized, "Yes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Hot", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Heated", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsHotKeyword(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.IndexOf("hot", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int? ResolveModifierTaxProfileId(ProductItemModel product, ModifierModel group, ModifierItemModel modifierItem, int? inheritedProfileId = null)
        {
            if (group?.IsTaxInherited == true)
            {
                if (inheritedProfileId.HasValue)
                {
                    return inheritedProfileId;
                }

                if (product?.TaxProfileId.HasValue == true)
                {
                    return product.TaxProfileId;
                }

                return ResolveFallbackProfileId(product);
            }

            if (modifierItem?.TaxProfileId.HasValue == true)
            {
                return modifierItem.TaxProfileId;
            }

            return ResolveFallbackProfileId(product, includeProductProfile: false);
        }

        private static int? ResolveFallbackProfileId(ProductItemModel product, bool includeProductProfile = true)
        {
            if (includeProductProfile && product?.TaxProfileId.HasValue == true)
            {
                return product.TaxProfileId;
            }

            var fromChildren = includeProductProfile ? ResolveProfileFromChildren(product) : null;
            if (fromChildren.HasValue)
            {
                return fromChildren;
            }

            var shop = GlobalDataService.Instance?.ShopDetails;
            var configuredProfiles = shop?.TaxProfiles;
            if (configuredProfiles != null && configuredProfiles.Count > 0)
            {
                var highestProfile = configuredProfiles
                    .Select(p => new
                    {
                        Profile = p,
                        HighestRate = p?.TaxRules?.Max(r => r?.Tax?.Rate ?? 0m) ?? 0m
                    })
                    .OrderByDescending(x => x.HighestRate)
                    .ThenByDescending(x => x.Profile.Id)
                    .FirstOrDefault();

                if (highestProfile != null && highestProfile.HighestRate > 0m)
                {
                    return highestProfile.Profile.Id;
                }

                return configuredProfiles.First().Id;
            }

            return null;
        }

        private static int? ResolveProfileFromChildren(ProductItemModel product)
        {
            if (product?.Modifiers == null) return null;

            foreach (var group in product.Modifiers)
            {
                if (group?.ModifierItems == null) continue;
                foreach (var mod in group.ModifierItems)
                {
                    if (mod?.TaxProfileId.HasValue == true)
                    {
                        return mod.TaxProfileId;
                    }

                    if (mod?.NestedModifiers == null) continue;
                    foreach (var nestedGroup in mod.NestedModifiers)
                    {
                        if (nestedGroup?.ModifierItems == null) continue;
                        var nestedMatch = nestedGroup.ModifierItems.FirstOrDefault(nmi => nmi?.TaxProfileId.HasValue == true);
                        if (nestedMatch != null)
                        {
                            return nestedMatch.TaxProfileId;
                        }
                    }
                }
            }

            return null;
        }
    }
}

