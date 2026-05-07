using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class NestedModifiersDialogViewModel : INotifyPropertyChanged
    {
        public List<ModifierModel> ModifierGroups { get; set; }
        
        // Changed from Dictionary<int, string> to Dictionary<int, List<string>> to support multiple selections
        private Dictionary<int, List<string>> _selectedNestedItems = new Dictionary<int, List<string>>();
        private string _parentItemName;
        
        public NestedModifiersDialogViewModel(List<ModifierModel> nestedModifierGroups, string parentItemName)
        {
            ModifierGroups = nestedModifierGroups ?? new List<ModifierModel>();
            ParentItemName = parentItemName;
            
            // Initialize empty selections for each nested modifier group
            foreach (var modifier in ModifierGroups)
            {
                _selectedNestedItems[modifier.Id] = new List<string>();
            }
            
            SaveCommand = new CashierRelayCommand(Save, () => CanProceed);
            CancelCommand = new CashierRelayCommand(Cancel);
            SelectNestedModifierCommand = new CashierRelayCommand<string>(OnSelectNestedModifier);
        }

        public NestedModifiersDialogViewModel(List<ModifierModel> nestedModifierGroups, string parentItemName, Dictionary<int, List<string>> preSelectedNestedModifiers)
        {
            ModifierGroups = nestedModifierGroups ?? new List<ModifierModel>();
            ParentItemName = parentItemName;
            
            // Initialize selections for each nested modifier group
            foreach (var modifier in ModifierGroups)
            {
                _selectedNestedItems[modifier.Id] = new List<string>();
            }
            
            // Pre-select nested modifiers if provided
            if (preSelectedNestedModifiers != null)
                {
                foreach (var kvp in preSelectedNestedModifiers)
                {
                    if (_selectedNestedItems.ContainsKey(kvp.Key) && kvp.Value != null)
                    {
                        _selectedNestedItems[kvp.Key] = new List<string>(kvp.Value);
                        
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
            
            SaveCommand = new CashierRelayCommand(Save, () => CanProceed);
            CancelCommand = new CashierRelayCommand(Cancel);
            SelectNestedModifierCommand = new CashierRelayCommand<string>(OnSelectNestedModifier);
        }

        public string ParentItemName
        {
            get => _parentItemName;
            set { _parentItemName = value; OnPropertyChanged(nameof(ParentItemName)); }
        }

        public List<string> GetSelectedNestedItems(int modifierId)
        {
            return _selectedNestedItems.TryGetValue(modifierId, out List<string> selected) ? selected : new List<string>();
        }

        public void SetSelectedNestedItem(int modifierId, string itemName)
        {
            if (!_selectedNestedItems.ContainsKey(modifierId))
            {
                _selectedNestedItems[modifierId] = new List<string>();
            }
            
            var currentSelections = _selectedNestedItems[modifierId];
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
                    // Allow deselection for nested modifiers even for radio button groups
                    // This allows users to clear their selections
                    currentSelections.Remove(itemName);
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
            
            OnPropertyChanged(nameof(SelectedNestedItems));
            OnPropertyChanged(nameof(SelectedNestedModifierDetails));
            OnPropertyChanged(nameof(CanProceed));
        }

        public Dictionary<int, List<string>> SelectedNestedItems => _selectedNestedItems;

        // Property to check if user can proceed (Save button enabled)
        public bool CanProceed
        {
            get
            {
                if (ModifierGroups == null) return true;
                
                foreach (var group in ModifierGroups)
                {
                    var selectedCount = GetSelectedNestedItems(group.Id).Count;
                    // Enforce minimum selection requirement for required groups (MinPermitted > 0)
                    if (group.MinPermitted > 0 && selectedCount < group.MinPermitted)
                    {
                        return false;
                    }
                    
                    // Check maximum limit
                    if (group.MaxPermitted > 0 && selectedCount > group.MaxPermitted)
                    {
                        return false;
                    }
                }
                
                return true;
            }
        }

        // Expose selected nested modifier details for summary
        public List<string> SelectedNestedModifierDetails
        {
            get
            {
                var details = new List<string>();
                if (ModifierGroups != null && SelectedNestedItems != null)
                {
                    foreach (var group in ModifierGroups)
                    {
                        var selectedItems = GetSelectedNestedItems(group.Id);
                        if (selectedItems.Count > 0)
                        {
                            foreach (var selectedName in selectedItems)
                        {
                            var selected = group.ModifierItems?.FirstOrDefault(item => item.ItemName == selectedName);
                            if (selected != null)
                            {
                                details.Add($"{group.Title}: {selected.ItemName}   ${selected.ItemPrice:0.00}");
                                }
                            }
                        }
                    }
                }
                return details;
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectNestedModifierCommand { get; }
        
        public event Action<Dictionary<int, string>> NestedModifierSaved;
        public event Action DialogClosed;
        
        // Convert the new multiple selection format to the expected Dictionary<int, string> format for backward compatibility
        public Dictionary<int, string> GetSelectedNestedItemsForBackwardCompatibility()
        {
            var result = new Dictionary<int, string>();
            foreach (var kvp in _selectedNestedItems)
            {
                if (kvp.Value.Count > 0)
                {
                    result[kvp.Key] = string.Join(", ", kvp.Value);
                }
            }
            return result;
        }
        
        private void OnSelectNestedModifier(string parameter)
        {
            // Parameter format: "groupId:itemId:itemName"
            var parts = parameter.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[0], out int groupId) && int.TryParse(parts[1], out int itemId))
            {
                var group = ModifierGroups.FirstOrDefault(g => g.Id == groupId);
                var item = group?.ModifierItems?.FirstOrDefault(i => i.Id == itemId);
                if (item != null)
                {
                    SetSelectedNestedItem(groupId, parts[2]);
                }
            }
        }
        
        private void Save()
        {
            // Convert to backward compatible format for the event
            var backwardCompatibleSelection = GetSelectedNestedItemsForBackwardCompatibility();
            NestedModifierSaved?.Invoke(backwardCompatibleSelection);
            DialogClosed?.Invoke();
        }
        
        private void Cancel()
        {
            // Treat cancel as "save with no selections" to allow clearing nested picks
            NestedModifierSaved?.Invoke(new Dictionary<int, string>());
            DialogClosed?.Invoke();
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
} 