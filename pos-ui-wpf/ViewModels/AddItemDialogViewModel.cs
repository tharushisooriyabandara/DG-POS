using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.ViewModels;
using POS_UI.Models;
using System.Collections.Generic;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class AddItemDialogViewModel : INotifyPropertyChanged
    {
        public string ProductName { get; set; }
        public decimal BasePrice { get; set; }
        public ProductItemModel Product { get; set; }

        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set { _isEditMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ActionButtonText)); }
        }
        public string ActionButtonText => IsEditMode ? "Update Item" : "Add Item";
        


        
        public string ModifierTitle => Product?.Modifiers?.FirstOrDefault()?.Title ?? "#####";
        public List<ModifierItemModel> ModifierItems => Product?.Modifiers?.FirstOrDefault()?.ModifierItems ?? new List<ModifierItemModel>();
        public bool HasModifiers => Product?.Modifiers != null && Product.Modifiers.Count > 0 && 
                                   Product.Modifiers.FirstOrDefault()?.ModifierItems != null && 
                                   Product.Modifiers.FirstOrDefault().ModifierItems.Count > 0;
        
        public bool HasSelectedModifiers => SelectedModifierDetails != null && SelectedModifierDetails.Count > 0;
        
        public List<ModifierDetailModel> StructuredModifierDetails
        {
            get
            {
                var details = new List<ModifierDetailModel>();
                if (Product?.Modifiers != null)
                {
                    foreach (var group in Product.Modifiers)
                    {
                        // Check for multiple selections first (new format)
                        if (SelectedModifiersMultiple.TryGetValue(group.Id, out var selectedNames) && selectedNames != null && selectedNames.Count > 0)
                        {
                            foreach (var selectedName in selectedNames)
                            {
                                var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                if (selected != null)
                                {
                                    details.Add(new ModifierDetailModel($"{group.Title}: {selected.ItemName}", selected.ItemPrice));
                                    
                                    // If this specific modifier item has nested modifier details, add them indented
                                    if (_nestedModifierDetails.TryGetValue(selected.ItemName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                    {
                                        foreach (var nested in nestedList)
                                        {
                                            // Parse nested modifier details to extract price
                                            var parts = nested.Split('$');
                                            if (parts.Length >= 2)
                                            {
                                                var nestedName = parts[0].Trim();
                                                if (decimal.TryParse(parts[1].Trim(), out decimal nestedPrice))
                                                {
                                                    details.Add(new ModifierDetailModel($"↳ {nestedName}", nestedPrice, true, "    "));
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        // Fallback to single selection (backward compatibility)
                        else if (SelectedModifiers.TryGetValue(group.Id, out var selectedName))
                        {
                            var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                            if (selected != null)
                            {
                                details.Add(new ModifierDetailModel($"{group.Title}: {selected.ItemName}", selected.ItemPrice));
                                // If this specific modifier item has nested modifier details, add them indented
                                if (_nestedModifierDetails.TryGetValue(selected.ItemName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                {
                                    foreach (var nested in nestedList)
                                    {
                                        // Parse nested modifier details to extract price
                                        var parts = nested.Split('$');
                                        if (parts.Length >= 2)
                                        {
                                            var nestedName = parts[0].Trim();
                                            if (decimal.TryParse(parts[1].Trim(), out decimal nestedPrice))
                                            {
                                                details.Add(new ModifierDetailModel($"↳ {nestedName}", nestedPrice, true, "    "));
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
        
        public double DialogHeight => (HasModifiers && HasSelectedModifiers) ? 530.0 : 410.0;
        
        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set { if (value < 1) value = 1; _quantity = value; OnPropertyChanged(); OnPropertyChanged(nameof(FinalPrice)); OnPropertyChanged(nameof(DiscountAmount)); OnPropertyChanged(nameof(TotalModifierPrice)); OnPropertyChanged(nameof(ItemSubTotal)); }
        }
        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set { _discountPercent = value;
             OnPropertyChanged(); 
             OnPropertyChanged(nameof(FinalPrice)); 
             OnPropertyChanged(nameof(DiscountAmount));
             OnPropertyChanged(nameof(DiscountPercent));
             OnPropertyChanged(nameof(UnitPrice));

             }
        }
        
        public ObservableCollection<DiscountPresetItem> DiscountPresets { get; } = new ObservableCollection<DiscountPresetItem>();
        public ICommand SelectPresetDiscountCommand { get; }

        private decimal _selectedPresetValue;

        private void InitDiscountPresets()
        {
            var configured = POS_UI.Services.GlobalDataService.Instance.ItemDiscountPresets;
            DiscountPresets.Clear();
            foreach (var pct in configured)
            {
                if (pct > 0 && pct <= 100)
                    DiscountPresets.Add(new DiscountPresetItem { Value = pct, Label = pct.ToString("G29") + "%" });
            }
        }

        private void SelectPresetDiscount(decimal value)
        {
            if (_selectedPresetValue == value)
            {
                _selectedPresetValue = 0;
                DiscountPercent = 0;
            }
            else
            {
                _selectedPresetValue = value;
                IsCustomDiscountActive = false;
                _customDiscountText = "";
                OnPropertyChanged(nameof(CustomDiscountText));
                DiscountPercent = value;
            }
            RefreshPresetSelections();
        }

        private void RefreshPresetSelections()
        {
            foreach (var p in DiscountPresets)
                p.IsSelected = p.Value == _selectedPresetValue;
        }

        public bool IsDiscount10Selected
        {
            get => Math.Abs(_selectedPresetValue - 10) < 0.1m;
            set
            {
                if (value) { _selectedPresetValue = 10; DiscountPercent = 10; }
                else if (Math.Abs(_selectedPresetValue - 10) < 0.1m) { _selectedPresetValue = 0; DiscountPercent = 0; }
                RefreshPresetSelections();
            }
        }
        public bool IsDiscount20Selected
        {
            get => Math.Abs(_selectedPresetValue - 20) < 0.1m;
            set
            {
                if (value) { _selectedPresetValue = 20; DiscountPercent = 20; }
                else if (Math.Abs(_selectedPresetValue - 20) < 0.1m) { _selectedPresetValue = 0; DiscountPercent = 0; }
                RefreshPresetSelections();
            }
        }

        private bool _isCustomDiscountActive;
        public bool IsCustomDiscountActive
        {
            get => _isCustomDiscountActive;
            set { _isCustomDiscountActive = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCustomDiscountActiveString)); }
        }
        public string IsCustomDiscountActiveString => _isCustomDiscountActive ? "True" : "False";

        private string _customDiscountText = "";
        public string CustomDiscountText
        {
            get => _customDiscountText;
            set
            {
                _customDiscountText = value;
                OnPropertyChanged();
                if (decimal.TryParse(value, out decimal pct) && pct >= 0)
                {
                    if (pct > 100) pct = 100;
                    _selectedPresetValue = 0;
                    RefreshPresetSelections();
                    IsCustomDiscountActive = pct > 0;
                    DiscountPercent = pct;
                }
                else if (string.IsNullOrWhiteSpace(value))
                {
                    if (IsCustomDiscountActive)
                    {
                        IsCustomDiscountActive = false;
                        DiscountPercent = 0;
                    }
                }
            }
        }

        public string Note { get; set; }
        public ICommand IncrementQtyCommand { get; }
        public ICommand DecrementQtyCommand { get; }
        public ICommand Discount10Command { get; }
        public ICommand Discount20Command { get; }
        public ICommand AddItemCommand { get; }
        public ICommand CloseCommand { get; }

        public ICommand OpenModifiersDialogCommand { get; }

        public decimal UnitPrice => BasePrice;
        public decimal DiscountAmount => Math.Round((BasePrice + BaseModifierPrice) * Quantity * DiscountPercent / 100, 2, MidpointRounding.AwayFromZero);

        public decimal ItemSubTotal => Math.Round((BasePrice + BaseModifierPrice) * Quantity, 2, MidpointRounding.AwayFromZero);


        public decimal BaseModifierPrice
        {
            get
            {
                decimal total = 0;
                
                // Calculate price from main modifiers
                if (Product?.Modifiers != null)
                {
                    foreach (var group in Product.Modifiers)
                    {
                        // Check for multiple selections first (new format)
                        if (SelectedModifiersMultiple.TryGetValue(group.Id, out var selectedNames) && selectedNames != null && selectedNames.Count > 0)
                        {
                            foreach (var selectedName in selectedNames)
                            {
                                var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                if (selected != null)
                                {
                                    total += selected.ItemPrice;
                                }
                            }
                        }
                        // Fallback to single selection (backward compatibility)
                        else if (SelectedModifiers.TryGetValue(group.Id, out var selectedName))
                        {
                            var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                            if (selected != null)
                            {
                                total += selected.ItemPrice;
                            }
                        }
                    }
                }
                
                // Calculate price from nested modifiers
                if (_nestedModifierDetails != null)
                {
                    foreach (var nestedList in _nestedModifierDetails.Values)
                    {
                        if (nestedList != null)
                        {
                            foreach (var nested in nestedList)
                            {
                                // Parse nested modifier details to extract price
                                var parts = nested.Split('$');
                                if (parts.Length >= 2)
                                {
                                    if (decimal.TryParse(parts[1].Trim(), out decimal nestedPrice))
                                    {
                                        total += nestedPrice;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return total;
            }
        }
        
        public decimal TotalModifierPrice => BaseModifierPrice * Quantity;
        

        public decimal FinalPrice => Math.Round((BasePrice + BaseModifierPrice) * Quantity - DiscountAmount, 2, MidpointRounding.AwayFromZero);
        public event Action<AddItemDialogViewModel> ItemAdded;
        public event Action DialogClosed;
        
        public AddItemDialogViewModel(string productName, decimal basePrice, ProductItemModel product = null)
        {
            ProductName = productName;
            BasePrice = basePrice;
            Product = product;
            
            IncrementQtyCommand = new RelayCommand(() => { Quantity++; });
            DecrementQtyCommand = new RelayCommand(() => { if (Quantity > 1) Quantity--; });
            SelectPresetDiscountCommand = new RelayCommand<decimal>(SelectPresetDiscount);
            Discount10Command = new RelayCommand(() => SelectPresetDiscount(10));
            Discount20Command = new RelayCommand(() => SelectPresetDiscount(20));
            InitDiscountPresets();
            AddItemCommand = new RelayCommand(() => { ItemAdded?.Invoke(this); DialogClosed?.Invoke(); });
            CloseCommand = new RelayCommand(() => { DialogClosed?.Invoke(); });
            OpenModifiersDialogCommand = new RelayCommand(OpenModifiersDialog);
            

        }
        
        public AddItemDialogViewModel(string productName, decimal basePrice, ProductItemModel product = null, Dictionary<int, string> selectedModifiers = null)
            : this(productName, basePrice, product)
        {
            if (selectedModifiers != null)
                SelectedModifiers = new Dictionary<int, string>(selectedModifiers);
        }
        
        public AddItemDialogViewModel(string productName, decimal basePrice, ProductItemModel product = null, Dictionary<int, string> selectedModifiers = null, Dictionary<string, List<string>> nestedModifierDetails = null)
            : this(productName, basePrice, product, selectedModifiers)
        {
            if (nestedModifierDetails != null)
                _nestedModifierDetails = new Dictionary<string, List<string>>(nestedModifierDetails);
        }

        // New constructor for multiple selections
        public AddItemDialogViewModel(string productName, decimal basePrice, ProductItemModel product = null, Dictionary<int, List<string>> selectedModifiersMultiple = null, Dictionary<string, List<string>> nestedModifierDetails = null)
            : this(productName, basePrice, product)
        {
            if (selectedModifiersMultiple != null)
                SelectedModifiersMultiple = new Dictionary<int, List<string>>(selectedModifiersMultiple);
            if (nestedModifierDetails != null)
                _nestedModifierDetails = new Dictionary<string, List<string>>(nestedModifierDetails);
        }
        

        



        // Store all selected modifiers from AddModifiersDialog
        private Dictionary<int, string> _selectedModifiers = new Dictionary<int, string>();
        public Dictionary<int, string> SelectedModifiers
        {
            get => _selectedModifiers;
            set { _selectedModifiers = value; OnPropertyChanged(nameof(SelectedModifiers)); OnPropertyChanged(nameof(SelectedModifierDetails)); OnPropertyChanged(nameof(StructuredModifierDetails)); OnPropertyChanged(nameof(BaseModifierPrice)); OnPropertyChanged(nameof(TotalModifierPrice)); OnPropertyChanged(nameof(FinalPrice)); OnPropertyChanged(nameof(DiscountAmount)); OnPropertyChanged(nameof(ItemSubTotal)); OnPropertyChanged(nameof(DialogHeight)); OnPropertyChanged(nameof(HasSelectedModifiers)); }
        }

        // Store multiple selected modifiers from AddModifiersDialog (new format)
        private Dictionary<int, List<string>> _selectedModifiersMultiple = new Dictionary<int, List<string>>();
        public Dictionary<int, List<string>> SelectedModifiersMultiple
        {
            get => _selectedModifiersMultiple;
            set { _selectedModifiersMultiple = value; OnPropertyChanged(nameof(SelectedModifiersMultiple)); OnPropertyChanged(nameof(SelectedModifierDetails)); OnPropertyChanged(nameof(StructuredModifierDetails)); OnPropertyChanged(nameof(BaseModifierPrice)); OnPropertyChanged(nameof(TotalModifierPrice)); OnPropertyChanged(nameof(FinalPrice)); OnPropertyChanged(nameof(DiscountAmount)); OnPropertyChanged(nameof(ItemSubTotal)); OnPropertyChanged(nameof(DialogHeight)); OnPropertyChanged(nameof(HasSelectedModifiers)); }
        }

        // Store nested modifier selections: parentModifierItemName -> List<string> (details)
        private Dictionary<string, List<string>> _nestedModifierDetails = new Dictionary<string, List<string>>();
        
        public Dictionary<string, List<string>> NestedModifierDetails
        {
            get => _nestedModifierDetails;
            set { _nestedModifierDetails = value; OnPropertyChanged(nameof(NestedModifierDetails)); OnPropertyChanged(nameof(DialogHeight)); OnPropertyChanged(nameof(HasSelectedModifiers)); }
        }
        
        public void SetNestedModifierDetails(int groupId, List<string> details)
        {
            // This method is kept for backward compatibility but should not be used
            // Nested modifier details should be set using the parent modifier item name
            OnPropertyChanged(nameof(SelectedModifierDetails));
            OnPropertyChanged(nameof(DialogHeight));
            OnPropertyChanged(nameof(HasSelectedModifiers));
        }

        private async void OpenModifiersDialog()
        {
            try
            {
                // Pass the current selections to pre-populate the dialog
                var dialogVm = new AddModifiersDialogViewModel(
                    Product?.Modifiers ?? new List<ModifierModel>(), 
                    SelectedModifiersMultiple, 
                    _nestedModifierDetails
                );
                var dialog = new POS_UI.View.AddModifiersDialog { DataContext = dialogVm };

                
                // Handle nested modifier details updates
                dialogVm.ModifierSavedWithNested += (selectedModifiers, nestedModifierDetails) =>
                {
                    // Update nested modifier details regardless of whether main modifiers are selected
                    if (nestedModifierDetails != null)
                    {
                        _nestedModifierDetails = new Dictionary<string, List<string>>(nestedModifierDetails);
                    }
                    
                    if (selectedModifiers != null && selectedModifiers.Count > 0)
                    {
                        // Convert the backward compatible format to the new multiple selection format
                        var multipleSelections = new Dictionary<int, List<string>>();
                        foreach (var kvp in selectedModifiers)
                        {
                            var itemNames = kvp.Value.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                            multipleSelections[kvp.Key] = new List<string>(itemNames);
                        }
                        
                        SelectedModifiersMultiple = multipleSelections;
                        
                        // For backward compatibility, also set the old format with the first selected item
                        var firstSelected = selectedModifiers.Values.FirstOrDefault();
                        if (!string.IsNullOrEmpty(firstSelected))
                        {
                            var firstItemName = firstSelected.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

                        }
                    }
                    else
                    {
                        // Clear selections if no modifiers are selected
                        SelectedModifiersMultiple = new Dictionary<int, List<string>>();
                    }
                    
                                         OnPropertyChanged(nameof(SelectedModifierDetails));
                     OnPropertyChanged(nameof(StructuredModifierDetails));
                     OnPropertyChanged(nameof(TotalModifierPrice));
                     OnPropertyChanged(nameof(FinalPrice));
                     OnPropertyChanged(nameof(DialogHeight));
                     OnPropertyChanged(nameof(BaseModifierPrice));
                     OnPropertyChanged(nameof(HasSelectedModifiers));
                };
                
                // Handle nested modifiers with pre-selection
                dialogVm.NestedModifierRequested += async (modifierItem) =>
                {
                    try
                    {
                        // Get previously selected nested modifiers for this specific modifier item
                        var preSelectedNestedModifiers = new Dictionary<int, List<string>>();
                        if (_nestedModifierDetails.TryGetValue(modifierItem.ItemName, out var nestedDetails))
                        {
                            // Convert the nested details back to the format expected by NestedModifiersDialogViewModel
                            foreach (var group in modifierItem.NestedModifiers)
                            {
                                var selectedItems = new List<string>();
                                foreach (var detail in nestedDetails)
                                {
                                    if (detail.StartsWith($"{group.Title}:"))
                                    {
                                        var itemName = detail.Substring(detail.IndexOf(':') + 1).Split('$')[0].Trim();
                                        selectedItems.Add(itemName);
                                    }
                                }
                                if (selectedItems.Count > 0)
                                {
                                    preSelectedNestedModifiers[group.Id] = selectedItems;
                                }
                            }
                        }
                        
                        var nestedDialogVm = new NestedModifiersDialogViewModel(
                            modifierItem.NestedModifiers, 
                            modifierItem.ItemName,
                            preSelectedNestedModifiers
                        );
                        var nestedDialog = new POS_UI.View.NestedModifiersDialog { DataContext = nestedDialogVm };
                        
                        Dictionary<int, string> selectedNestedModifiers = null;
                        nestedDialogVm.NestedModifierSaved += (nestedModifiers) =>
                        {
                            selectedNestedModifiers = nestedModifiers;
                        };
                        nestedDialogVm.DialogClosed += () => MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                        
                        await MaterialDesignThemes.Wpf.DialogHost.Show(nestedDialog, "NestedModifiersDialogHost");
                        
                        if (selectedNestedModifiers != null)
                        {
                            // Format nested details for summary
                            var formattedNestedDetails = new List<string>();
                            foreach (var group in modifierItem.NestedModifiers)
                            {
                                // Handle multiple selections from nested modifiers
                                if (selectedNestedModifiers.TryGetValue(group.Id, out var selectedNames))
                                {
                                    // Split the comma-separated string into individual selections
                                    var itemNames = selectedNames.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var selectedName in itemNames)
                                    {
                                        var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                        if (selected != null)
                                        {
                                            formattedNestedDetails.Add($"{group.Title}: {selected.ItemName}   ${selected.ItemPrice:0.00}");
                                        }
                                    }
                                }
                            }
                            
                                                         // Find the parent groupId for this modifierItem
                             var parentGroup = dialogVm.ModifierGroups.FirstOrDefault(g => g.ModifierItems != null && g.ModifierItems.Contains(modifierItem));
                             if (parentGroup != null)
                             {
                                 dialogVm.SetNestedModifierDetails(parentGroup.Id, formattedNestedDetails, modifierItem);
                             }
                             
                             // Update the nested modifier details in this view model
                             if (formattedNestedDetails.Count > 0)
                             {
                                 _nestedModifierDetails[modifierItem.ItemName] = formattedNestedDetails;
                                 
                                 // Ensure the parent modifier item is selected if it has nested modifiers
                                 var currentSelections = SelectedModifiersMultiple.ContainsKey(parentGroup.Id) 
                                     ? new List<string>(SelectedModifiersMultiple[parentGroup.Id]) 
                                     : new List<string>();
                                 
                                 if (!currentSelections.Contains(modifierItem.ItemName))
                                 {
                                     currentSelections.Add(modifierItem.ItemName);
                                     SelectedModifiersMultiple[parentGroup.Id] = currentSelections;
                                 }
                             }
                             else
                             {
                                 // Remove nested modifier details if no selections
                                 _nestedModifierDetails.Remove(modifierItem.ItemName);
                             }
                             
                             // Trigger property changes to update the UI
                             OnPropertyChanged(nameof(SelectedModifierDetails));
                             OnPropertyChanged(nameof(StructuredModifierDetails));
                             OnPropertyChanged(nameof(BaseModifierPrice));
                             OnPropertyChanged(nameof(TotalModifierPrice));
                             OnPropertyChanged(nameof(FinalPrice));
                             OnPropertyChanged(nameof(DialogHeight));
                             OnPropertyChanged(nameof(HasSelectedModifiers));
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"Error opening nested modifiers dialog: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                };
                
                dialogVm.DialogClosed += () => MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);
                await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "ModifiersDialogHost");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening AddModifiersDialog: {ex.Message}\n\n{ex.StackTrace}", "Dialog Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // Updated summary property to include nested modifier details and multiple selections
        public List<string> SelectedModifierDetails
        {
            get
            {
                var details = new List<string>();
                if (Product?.Modifiers != null)
                {
                    foreach (var group in Product.Modifiers)
                    {
                        // Check for multiple selections first (new format)
                        if (SelectedModifiersMultiple.TryGetValue(group.Id, out var selectedNames) && selectedNames != null && selectedNames.Count > 0)
                        {
                            foreach (var selectedName in selectedNames)
                            {
                                var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                                if (selected != null)
                                {
                                    var line = $"{group.Title}: {selected.ItemName}  \t ${selected.ItemPrice:0.00}";
                                    details.Add(line);
                                    
                                    // If this specific modifier item has nested modifier details, add them indented
                                    if (_nestedModifierDetails.TryGetValue(selected.ItemName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                    {
                                        foreach (var nested in nestedList)
                                        {
                                            details.Add($"\t↳ {nested}");
                                        }
                                    }
                                }
                            }
                        }
                        // Fallback to single selection (backward compatibility)
                        else if (SelectedModifiers.TryGetValue(group.Id, out var selectedName))
                        {
                            var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                            if (selected != null)
                            {
                                var line = $"{group.Title}: {selected.ItemName}  \t ${selected.ItemPrice:0.00}";
                                details.Add(line);
                                // If this specific modifier item has nested modifier details, add them indented
                                if (_nestedModifierDetails.TryGetValue(selected.ItemName, out var nestedList) && nestedList != null && nestedList.Count > 0)
                                {
                                    foreach (var nested in nestedList)
                                    {
                                        details.Add($"\t↳ {nested}");
                                    }
                                }
                            }
                        }
                    }
                }
                return details;
            }
        }
    }

    public class DiscountPresetItem : INotifyPropertyChanged
    {
        public decimal Value { get; set; }
        public string Label { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelectedString))); }
        }
        public string IsSelectedString => _isSelected ? "True" : "False";

        public event PropertyChangedEventHandler PropertyChanged;
    }
} 