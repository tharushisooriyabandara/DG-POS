using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class AddModifiersDialogViewModel : INotifyPropertyChanged
    {
        public List<ModifierModel> ModifierGroups { get; set; }
        
        // Changed from Dictionary<int, string> to Dictionary<int, List<string>> to support multiple selections
        private Dictionary<int, List<string>> _selectedItems = new Dictionary<int, List<string>>();
        
        // Store nested modifier selections: parentModifierItemName -> List<string> (details)
        private Dictionary<string, List<string>> _nestedModifierDetails = new Dictionary<string, List<string>>();
        
        public void SetNestedModifierDetails(int groupId, List<string> details, ModifierItemModel parentModifierItem = null)
        {
            if (parentModifierItem != null)
            {
                if (details != null && details.Count > 0)
                {
                    // Store nested modifier details
                    _nestedModifierDetails[parentModifierItem.ItemName] = details;
                    
                    // Automatically select the parent modifier item if nested modifiers are selected
                    var currentSelections = GetSelectedItems(groupId);
                    if (!currentSelections.Contains(parentModifierItem.ItemName))
                    {
                        SetSelectedItem(groupId, parentModifierItem.ItemName);
                    }
                }
                else
                {
                    // Remove nested modifier details if no selections
                    _nestedModifierDetails.Remove(parentModifierItem.ItemName);
                    
                    // Deselect the parent modifier item if no nested modifiers are selected
                    var currentSelections = GetSelectedItems(groupId);
                    if (currentSelections.Contains(parentModifierItem.ItemName))
                    {
                        currentSelections.Remove(parentModifierItem.ItemName);
                        
                        // Update IsSelected property for the parent item
                        var modifierGroup = ModifierGroups.FirstOrDefault(g => g.Id == groupId);
                        if (modifierGroup != null)
                        {
                            var parentItem = modifierGroup.ModifierItems?.FirstOrDefault(item => item.ItemName == parentModifierItem.ItemName);
                            if (parentItem != null)
                            {
                                parentItem.IsSelected = false;
                                // Force UI refresh by triggering property change on the group
                                modifierGroup.OnPropertyChanged(nameof(ModifierModel.ModifierItems));
                            }
                        }
                    }
                }
            }
            
            OnPropertyChanged(nameof(SelectedModifierDetails));
            OnPropertyChanged(nameof(CanProceed));
            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(ModifierGroups));
        }

        public AddModifiersDialogViewModel(List<ModifierModel> modifierGroups)
        {
            ModifierGroups = modifierGroups ?? new List<ModifierModel>();
            
            // Initialize empty selections for each modifier group
            foreach (var modifier in ModifierGroups)
            {
                _selectedItems[modifier.Id] = new List<string>();
            }
            
            SaveCommand = new CashierRelayCommand(Save, () => CanProceed);
            CancelCommand = new CashierRelayCommand(Cancel);
            SelectModifierItemCommand = new CashierRelayCommand<string>(OnSelectModifierItem);
        }

        public AddModifiersDialogViewModel(List<ModifierModel> modifierGroups, Dictionary<int, List<string>> preSelectedModifiers, Dictionary<string, List<string>> nestedModifierDetails = null)
        {
            ModifierGroups = modifierGroups ?? new List<ModifierModel>();
            
            // Initialize selections for each modifier group
            foreach (var modifier in ModifierGroups)
            {
                _selectedItems[modifier.Id] = new List<string>();
            }
            
            // Pre-select modifiers if provided
            if (preSelectedModifiers != null)
            {
                foreach (var kvp in preSelectedModifiers)
                {
                    if (_selectedItems.ContainsKey(kvp.Key) && kvp.Value != null)
                {
                        _selectedItems[kvp.Key] = new List<string>(kvp.Value);
                        
                        // Update IsSelected property for all items in this group
                        var modifierGroup = ModifierGroups.FirstOrDefault(g => g.Id == kvp.Key);
                        if (modifierGroup != null)
                        {
                            foreach (var item in modifierGroup.ModifierItems)
                            {
                                item.IsSelected = kvp.Value.Contains(item.ItemName);
                            }
                        }
                    }
                }
            }
            
            // Set nested modifier details if provided
            if (nestedModifierDetails != null)
                    {
                _nestedModifierDetails = new Dictionary<string, List<string>>(nestedModifierDetails);
            }
            
            SaveCommand = new CashierRelayCommand(Save, () => CanProceed);
            CancelCommand = new CashierRelayCommand(Cancel);
            SelectModifierItemCommand = new CashierRelayCommand<string>(OnSelectModifierItem);
        }

        public List<string> GetSelectedItems(int modifierId)
        {
            return _selectedItems.TryGetValue(modifierId, out List<string> selected) ? selected : new List<string>();
        }

        public void SetSelectedItem(int modifierId, string itemName)
        {
            if (!_selectedItems.ContainsKey(modifierId))
            {
                _selectedItems[modifierId] = new List<string>();
            }
            
            var currentSelections = _selectedItems[modifierId];
            var modifierGroup = ModifierGroups.FirstOrDefault(g => g.Id == modifierId);
            
            if (modifierGroup != null)
            {
                // Update IsSelected property for all items in this group
                foreach (var item in modifierGroup.ModifierItems)
                {
                    item.IsSelected = currentSelections.Contains(item.ItemName);
                }
                
                // Check if item is already selected
                if (currentSelections.Contains(itemName))
                {
                    // For radio button behavior (min=1, max=1), don't allow deselection
                    // For other cases, allow toggle behavior
                    if (modifierGroup.MinPermitted == 1 && modifierGroup.MaxPermitted == 1)
                    {
                        // Radio button behavior - item stays selected, no deselection allowed
                        // This ensures at least one item is always selected
                        return;
                    }
                    else
                    {
                        // Remove item if already selected (toggle behavior for other cases)
                        currentSelections.Remove(itemName);
                    }
                }
                else
                {
                    // Add item if not selected
                    if (modifierGroup.MaxPermitted == 1)
                    {
                        // Single selection - replace current selection (radio button behavior)
                        currentSelections.Clear();
                        currentSelections.Add(itemName);
                    }
                    else if (modifierGroup.MaxPermitted > 1)
                    {
                        // Multiple selection - check if we haven't exceeded max
                        if (currentSelections.Count < modifierGroup.MaxPermitted)
                        {
                            currentSelections.Add(itemName);
                        }
                    }
                    else
                    {
                        // No max limit
                        currentSelections.Add(itemName);
                    }
                }
                
                // Update IsSelected property for all items in this group after changes
                foreach (var item in modifierGroup.ModifierItems)
                {
                    item.IsSelected = currentSelections.Contains(item.ItemName);
                }
                
                // Trigger property change for the modifier group to update SelectionStatusText
                modifierGroup.OnPropertyChanged(nameof(ModifierModel.SelectionStatusText));
            }
            
            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(CanProceed));
            OnPropertyChanged(nameof(ModifierGroups));
        }

        public Dictionary<int, List<string>> SelectedItems => _selectedItems;

        // Property to check if user can proceed (Next button enabled)
        public bool CanProceed
        {
            get
            {
                if (ModifierGroups == null) return true;
                
                foreach (var group in ModifierGroups)
                {
                    var selectedCount = GetSelectedItems(group.Id).Count;
                    
                    // Check minimum requirement
                    if (group.MinPermitted > 0 && selectedCount < group.MinPermitted)
                    {
                        return false;
                    }
                    
                    // Check maximum limit
                    if (group.MaxPermitted > 0 && selectedCount > group.MaxPermitted)
                    {
                        return false;
                    }

                    // If any selected item has nested modifiers with required groups,
                    // ensure nested selections exist for that parent item before allowing save.
                    if (selectedCount > 0)
                    {
                        var selectedNames = GetSelectedItems(group.Id);
                        foreach (var selectedName in selectedNames)
                        {
                            var selectedItem = group.ModifierItems?.FirstOrDefault(i => i.ItemName == selectedName);
                            if (selectedItem != null && selectedItem.HasNestedModifiers)
                            {
                                // Determine if any nested modifier group is required
                                bool hasRequiredNestedGroup = selectedItem.NestedModifiers != null && selectedItem.NestedModifiers.Any(n => n.MinPermitted > 0);
                                if (hasRequiredNestedGroup)
                                {
                                    // Validate nested selections are present for this parent item
                                    if (!_nestedModifierDetails.TryGetValue(selectedItem.ItemName, out var nestedDetails)
                                        || nestedDetails == null
                                        || nestedDetails.Count == 0)
                                    {
                                        return false;
                                    }
                                }
                            }
                        }
                    }
                }
                
                return true;
            }
        }



        // Updated summary property to include nested modifier details
        public List<string> SelectedModifierDetails
        {
            get
            {
                var details = new List<string>();
                if (ModifierGroups != null && SelectedItems != null)
                {
                    foreach (var group in ModifierGroups)
                    {
                        var selectedItems = GetSelectedItems(group.Id);
                        if (selectedItems.Count > 0)
                        {
                            foreach (var selectedName in selectedItems)
                        {
                            var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                            if (selected != null)
                            {
                                var line = $"{group.Title}: {selected.ItemName}   ${selected.ItemPrice:0.00}";
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
                }
                return details;
            }
        }

        public Dictionary<string, List<string>> GetAllNestedModifierDetails()
        {
            return new Dictionary<string, List<string>>(_nestedModifierDetails);
        }

        // Convert the new multiple selection format to the expected Dictionary<int, string> format for backward compatibility
        public Dictionary<int, string> GetSelectedItemsForBackwardCompatibility()
        {
            var result = new Dictionary<int, string>();
            foreach (var kvp in _selectedItems)
            {
                if (kvp.Value.Count > 0)
                {
                    result[kvp.Key] = string.Join(", ", kvp.Value);
                }
            }
            return result;
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public ICommand SelectModifierItemCommand { get; }
        
        public event Action<Dictionary<int, string>> ModifierSaved;
        public event Action<Dictionary<int, string>, Dictionary<string, List<string>>> ModifierSavedWithNested;
        public event Action DialogClosed;
        public event Action<ModifierItemModel> NestedModifierRequested;
        

        
        private void OnSelectModifierItem(string parameter)
        {
            // Parameter format: "groupId:itemId:itemName"
            var parts = parameter.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[0], out int groupId) && int.TryParse(parts[1], out int itemId))
            {
                var modifierGroup = ModifierGroups.FirstOrDefault(g => g.Id == groupId);
                var modifierItem = modifierGroup?.ModifierItems?.FirstOrDefault(item => item.Id == itemId);
                if (modifierItem != null)
                {
                    if (modifierItem.HasNestedModifiers)
                    {
                        NestedModifierRequested?.Invoke(modifierItem);
                    }
                    else
                    {
                        SetSelectedItem(groupId, parts[2]);
                    }
                }
            }
        }
        
        private void Save()
        {
            // Convert to backward compatible format for the event
            var backwardCompatibleSelection = GetSelectedItemsForBackwardCompatibility();
            
            // Invoke both events for backward compatibility and new functionality
            ModifierSaved?.Invoke(backwardCompatibleSelection);
            ModifierSavedWithNested?.Invoke(backwardCompatibleSelection, _nestedModifierDetails);
            DialogClosed?.Invoke();
        }
        
        private void Cancel()
        {
            DialogClosed?.Invoke();
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
} 