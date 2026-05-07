using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq; // Added for FirstOrDefault

namespace POS_UI.Models
{
    public class OrderItem : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        private string _name;
        public string Name 
        { 
            get => _name; 
            set 
            { 
                _name = value; 
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(DisplayName));
            } 
        }
        public decimal ApiItemPrice { get; set; }
        // Computed property to get the display name (either from Product or Name property)
        public string DisplayName => Product?.ItemName ?? Name ?? "Unknown Item";
        public decimal _price;
        public decimal Price
        {
            get => _price;
            set { _price = value; 
            OnPropertyChanged(nameof(Price));
            OnPropertyChanged(nameof(Total)); }
        }
        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                if (_discountPercent != value)
                {
                    _discountPercent = value;
                    OnPropertyChanged(nameof(DiscountPercent));
                    OnPropertyChanged(nameof(DiscountAmount));
                    OnPropertyChanged(nameof(Total));
                }
            }
        }
        // Prefer percentage-based computation when a discount percent is set so it scales with quantity; otherwise fallback to API-provided amount
        public decimal DiscountAmount =>
            DiscountPercent > 0
                ? Math.Round(Price * Quantity * DiscountPercent / 100, 2, MidpointRounding.AwayFromZero)
                : ApiDiscountAmount;
        public decimal DisAmount { get; set; }
        public decimal UnitDiscountAmount { get; set; }
        private int _quantity;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    var previousQuantity = _quantity;
                    _quantity = value;
                    if (previousQuantity > 0 && _quantity > 0 && UnitDiscountAmount > 0m)
                    {
                        var newLineDiscount = Math.Round(UnitDiscountAmount * _quantity, 2, MidpointRounding.AwayFromZero);
                        DisAmount = newLineDiscount;
                        ApiDiscountAmount = newLineDiscount;
                        VisibleDiscountAmount = newLineDiscount;
                    }
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(Total));
                    OnPropertyChanged(nameof(DiscountAmount));
                }
            }
        }
        public string Notes { get; set; }
        public decimal Subtotal => (Price * Quantity) - DiscountAmount;
        public decimal Total => Price * Quantity;
        public decimal ApiDiscountAmount { get; set; }
        public decimal TotalPerItem => Price;  
        // API item identifier used when updating an order
        public int ApiItemId { get; set; }
        // Local dine-in identity to map cart items to local storage items
        public int? LocalItemId { get; set; }
        // Optional category name snapshot to preserve grouping for printing
        public string CategoryName { get; set; }
        // New fields for ViewModel compatibility
        private ProductItemModel _product;
        public ProductItemModel Product 
        { 
            get => _product; 
            set 
            { 
                _product = value; 
                OnPropertyChanged(nameof(Product));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(HasModifiers));
            } 
        }
        private string _note;
        public string Note
        {
            get => _note;
            set
            {
                // Enforce max length of 50 characters for item notes
                var normalized = value;
                if (!string.IsNullOrEmpty(normalized) && normalized.Length > 50)
                {
                    normalized = normalized.Substring(0, 50);
                }

                if (_note != normalized)
                {
                    _note = normalized;
                    OnPropertyChanged(nameof(Note));
                }
            }
        }

        private bool _isReadOnly;
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set
            {
                if (_isReadOnly != value)
                {
                    _isReadOnly = value;
                    OnPropertyChanged(nameof(IsReadOnly));
                }
            }
        }

        private string _originalStatus;
        public string OriginalStatus
        {
            get => _originalStatus;
            set
            {
                if (_originalStatus != value)
                {
                    _originalStatus = value;
                    OnPropertyChanged(nameof(OriginalStatus));
                }
            }
        }

        private string _itemStatus;
        public string ItemStatus
        {
            get => _itemStatus;
            set
            {
                if (_itemStatus != value)
                {
                    _itemStatus = value;
                    OnPropertyChanged(nameof(ItemStatus));
                }
            }
        }
        
        // Modifier details for display in order summary
        private Dictionary<int, List<string>> _selectedModifiers = new Dictionary<int, List<string>>();
        public Dictionary<int, List<string>> SelectedModifiers 
        { 
            get => _selectedModifiers; 
            set 
            { 
                _selectedModifiers = value; 
                OnPropertyChanged(nameof(SelectedModifiers));
                OnPropertyChanged(nameof(ModifierDetailsForDisplay));
                OnPropertyChanged(nameof(NestedModifierTotal));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(DiscountAmount));
            } 
        }
        
        private Dictionary<string, List<string>> _nestedModifierDetails = new Dictionary<string, List<string>>();
        public Dictionary<string, List<string>> NestedModifierDetails 
        { 
            get => _nestedModifierDetails; 
            set 
            { 
                _nestedModifierDetails = value; 
                OnPropertyChanged(nameof(NestedModifierDetails));
                OnPropertyChanged(nameof(ModifierDetailsForDisplay));
                OnPropertyChanged(nameof(NestedModifierTotal));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(DiscountAmount));
            } 
        }
        
        // Check if the item has any modifiers
        public bool HasModifiers => (Product?.Modifiers != null && Product.Modifiers.Count > 0);
        
        // Calculate total price of all selected modifiers
        /*public decimal TotalModifierPrice
        {
            get
            {
                decimal total = 0;
                // Calculate price from main modifiers (support multiple selections per group)
                if (Product?.Modifiers != null)
                {
                    foreach (var group in Product.Modifiers)
                    {
                        if (SelectedModifiers != null && SelectedModifiers.TryGetValue(group.Id, out var selectedNames) && selectedNames != null && selectedNames.Count > 0)
                        {
                            foreach (var selectedName in selectedNames)
                            {
                                var modifierItem = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                if (modifierItem != null)
                                {
                                    total += modifierItem.ItemPrice;
                                }
                                // Add nested modifier prices for this main modifier item
                                if (NestedModifierDetails != null && NestedModifierDetails.TryGetValue(selectedName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                {
                                    foreach (var nested in nestedList)
                                    {
                                        int dollarIndex = nested.LastIndexOf('$');
                                        if (dollarIndex >= 0 && dollarIndex < nested.Length - 1)
                                        {
                                            string pricePart = nested.Substring(dollarIndex + 1).Trim();
                                            if (decimal.TryParse(pricePart, out decimal nestedPrice))
                                            {
                                                total += nestedPrice;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return total;
            }
        }*/
        
        // Computed property to get formatted modifier details for display
        public List<ModifierDetailModel> ModifierDetailsForDisplay
        {
            get
            {
                var details = new List<ModifierDetailModel>();
                
                // Add main modifiers with their nested modifiers grouped together
                if (SelectedModifiers != null)
                {
                    foreach (var kvp in SelectedModifiers)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            // Find the modifier group by ID
                            var group = Product?.Modifiers?.FirstOrDefault(g => g.Id == kvp.Key);
                            string groupTitle = group?.Title ?? "Modifier";
                            
                            foreach (var modifierName in kvp.Value)
                            {
                                ModifierDetailModel mainModifier = null;
                                decimal itemPrice = 0;
                                
                                // Check if the modifier name is already in "Group: Item" format
                                if (modifierName.Contains(":"))
                                {
                                    // Already formatted, extract price
                                    var parts = modifierName.Split(':');
                                    if (parts.Length >= 2)
                                    {
                                        var existingGroupTitle = parts[0].Trim();
                                        var itemName = parts[1].Trim();
                                        
                                        // Try to find the price from the Product's modifiers
                                        var modifierItem = group?.ModifierItems?.FirstOrDefault(item => item.ItemName == itemName);
                                        if (modifierItem != null)
                                        {
                                            itemPrice = modifierItem.OriginalPrice > 0 ? modifierItem.OriginalPrice : modifierItem.ItemPrice;
                                        }
                                        
                                        mainModifier = new ModifierDetailModel($"{existingGroupTitle}: {itemName}", itemPrice);
                                    }
                                }
                                else
                                {
                                    // Not formatted, format it properly with group title
                                    var modifierItem = group?.ModifierItems?.FirstOrDefault(item => item.ItemName == modifierName);
                                    if (modifierItem != null)
                                    {
                                        itemPrice = modifierItem.OriginalPrice > 0 ? modifierItem.OriginalPrice : modifierItem.ItemPrice;
                                        mainModifier = new ModifierDetailModel($"{groupTitle}: {modifierItem.ItemName}", itemPrice);
                                    }
                                    else
                                    {
                                        // Fallback if item not found in group
                                        mainModifier = new ModifierDetailModel($"{groupTitle}: {modifierName}", 0);
                                    }
                                }
                                
                                // Add the main modifier
                                if (mainModifier != null)
                                {
                                    details.Add(mainModifier);
                                    
                                    // Add nested modifiers for this specific modifier item
                                    if (NestedModifierDetails != null && NestedModifierDetails.TryGetValue(modifierName, out var nestedList))
                                    {
                                        if (nestedList != null && nestedList.Count > 0)
                                        {
                                            foreach (var nestedDetail in nestedList)
                                            {
                                                // Parse nested modifier details to extract price
                                                var parts = nestedDetail.Split('$');
                                                if (parts.Length >= 2)
                                                {
                                                    var nestedName = parts[0].Trim();
                                                    if (decimal.TryParse(parts[1].Trim(), out decimal nestedPrice))
                                                    {
                                                        details.Add(new ModifierDetailModel($"↳ {nestedName}", nestedPrice, true, "    "));
                                                    }
                                                }
                                                else
                                                {
                                                    details.Add(new ModifierDetailModel($"↳ {nestedDetail}", 0, true, "    "));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                return details;
            }
        }

        // Returns the total of the parent modifier price plus all selected nested modifier prices for all selected nested modifiers.
        public decimal NestedModifierTotal
        {
            get
            {
                decimal sum = 0;
                
                // If we have Product and Modifiers, use the full calculation
                if (Product?.Modifiers != null && SelectedModifiers != null && NestedModifierDetails != null)
                {
                    foreach (var group in Product.Modifiers)
                    {
                        if (SelectedModifiers.TryGetValue(group.Id, out var selectedNames) && selectedNames != null && selectedNames.Count > 0)
                        {
                            foreach (var selectedName in selectedNames)
                            {
                                var modifierItem = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                if (modifierItem != null)
                                {
                                    // Only consider if this modifier has nested modifiers and any are selected
                                    if (modifierItem.HasNestedModifiers && NestedModifierDetails.TryGetValue(selectedName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                    {
                                        sum += modifierItem.ItemPrice; // Add parent modifier price
                                        foreach (var nested in nestedList)
                                        {
                                            int dollarIndex = nested.LastIndexOf('$');
                                            if (dollarIndex >= 0 && dollarIndex < nested.Length - 1)
                                            {
                                                string pricePart = nested.Substring(dollarIndex + 1).Trim();
                                                if (decimal.TryParse(pricePart, out decimal nestedPrice))
                                                {
                                                    sum += nestedPrice;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                // Fallback calculation for items without Product (e.g., from drafts)
                else if (NestedModifierDetails != null)
                {
                    foreach (var kvp in NestedModifierDetails)
                    {
                        if (kvp.Value != null && kvp.Value.Count > 0)
                        {
                            foreach (var nested in kvp.Value)
                            {
                                int dollarIndex = nested.LastIndexOf('$');
                                if (dollarIndex >= 0 && dollarIndex < nested.Length - 1)
                                {
                                    string pricePart = nested.Substring(dollarIndex + 1).Trim();
                                    if (decimal.TryParse(pricePart, out decimal nestedPrice))
                                    {
                                        sum += nestedPrice;
                                    }
                                }
                            }
                        }
                    }
                }
                return sum;
            }
        }

        // External modifier details for display (used when loading from API where Product/SelectedModifiers are not available)
        public List<ModifierDetailModel> ExternalModifierDetailsForDisplay { get; set; } = new List<ModifierDetailModel>();
        public Dictionary<string, TaxDetailModel> ExternalModifierTaxDetails { get; set; } = new Dictionary<string, TaxDetailModel>(StringComparer.OrdinalIgnoreCase);

        public void SetExternalModifierTaxDetail(string label, TaxDetailModel detail)
        {
            if (string.IsNullOrWhiteSpace(label) || detail == null)
            {
                return;
            }

            ExternalModifierTaxDetails[label.Trim()] = detail;
        }

        // Display-only discount amount used by cart UI when Price already reflects final amount
        private decimal _visibleDiscountAmount;
        public decimal VisibleDiscountAmount
        {
            get => _visibleDiscountAmount;
            set
            {
                var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                if (_visibleDiscountAmount != rounded)
                {
                    _visibleDiscountAmount = rounded;
                    if (_quantity > 0)
                    {
                        UnitDiscountAmount = Math.Round(_visibleDiscountAmount / _quantity, 2, MidpointRounding.AwayFromZero);
                    }
                    OnPropertyChanged(nameof(VisibleDiscountAmount));
                }
            }
        }

        public decimal BaseUnitPrice { get; set; }
        public List<OrderItemTaxComponent> TaxComponents { get; set; } = new List<OrderItemTaxComponent>();
        public List<TaxDetailModel> TaxDetails { get; set; } = new List<TaxDetailModel>();
        public decimal TaxAmount { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 