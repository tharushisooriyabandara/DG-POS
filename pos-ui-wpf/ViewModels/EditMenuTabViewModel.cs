using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    public class EditMenuTabViewModel : INotifyPropertyChanged
    {
        private string _tabName;
        private bool _isShowCategories = true;
        private bool _isShowItems;
        private string _itemSearchText;
        private string _categorySearchText;
        private ObservableCollection<SelectableItem> _filteredItems;
        private ObservableCollection<SelectableCategory> _filteredCategories;
        private int _activeSlotIndex = -1;
        private MenuTabModel _originalTab;

        private const int MIN_EMPTY_SLOTS = 6;

        public string Title { get; set; }

        public ObservableCollection<MenuGridSlot> MenuSlots { get; set; } = new ObservableCollection<MenuGridSlot>();

        public ObservableCollection<SelectableCategory> AvailableCategories { get; set; } = new ObservableCollection<SelectableCategory>();
        public ObservableCollection<SelectableItem> AvailableItems { get; set; } = new ObservableCollection<SelectableItem>();

        public ObservableCollection<SelectableItem> FilteredItems
        {
            get => _filteredItems;
            set { _filteredItems = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SelectableCategory> FilteredCategories
        {
            get => _filteredCategories;
            set { _filteredCategories = value; OnPropertyChanged(); }
        }

        public string TabName
        {
            get => _tabName;
            set { _tabName = value; OnPropertyChanged(); }
        }

        public bool IsShowCategories
        {
            get => _isShowCategories;
            set { _isShowCategories = value; OnPropertyChanged(); if (value) IsShowItems = false; }
        }

        public bool IsShowItems
        {
            get => _isShowItems;
            set { _isShowItems = value; OnPropertyChanged(); if (value) IsShowCategories = false; }
        }

        public string ItemSearchText
        {
            get => _itemSearchText;
            set { _itemSearchText = value; OnPropertyChanged(); FilterItems(); }
        }

        public string CategorySearchText
        {
            get => _categorySearchText;
            set { _categorySearchText = value; OnPropertyChanged(); FilterCategories(); }
        }

        public int ActiveSlotIndex
        {
            get => _activeSlotIndex;
            set
            {
                _activeSlotIndex = value;
                OnPropertyChanged();
                UpdateSlotActiveStates();
            }
        }

        public int PlacedCount => MenuSlots.Count(s => !s.IsEmpty);
        public bool HasPlacedItems => PlacedCount > 0;
        public string PlacedLabel => PlacedCount == 1 ? "item placed" : "items placed";

        /// <summary>Available palette colors for the color picker.</summary>
        public List<string> AvailableColors { get; } = POS_UI.Helpers.ColorPalette.GetAllColors();

        // Commands
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ToggleCategoriesCommand { get; }
        public ICommand ToggleItemsCommand { get; }
        public ICommand AssignCategoryCommand { get; }
        public ICommand AssignItemCommand { get; }
        public ICommand ActivateSlotCommand { get; }
        public ICommand RemoveSlotCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand ToggleColorPickerCommand { get; }
        public ICommand ChangeSlotColorCommand { get; }

        // Callbacks
        public Func<MenuTabModel, System.Threading.Tasks.Task> OnSave { get; set; }
        public Action OnCancel { get; set; }

        public EditMenuTabViewModel(MenuTabModel tab = null, ObservableCollection<string> categories = null, ObservableCollection<ProductItemModel> products = null)
        {
            Title = tab == null ? "Add New Tab" : "Edit Tab";
            _originalTab = tab;

            if (tab != null)
            {
                TabName = tab.Name;
            }

            var categoryMap = new Dictionary<string, int>();
            if (products != null)
            {
                foreach (var product in products)
                {
                    if (!string.IsNullOrEmpty(product.Category) && !categoryMap.ContainsKey(product.Category))
                    {
                        categoryMap[product.Category] = product.CategoryId;
                    }
                }
            }

            int fallbackId = 1;
            if (categories != null)
            {
                foreach (var cat in categories.Where(c => c != "All Items"))
                {
                    var id = categoryMap.ContainsKey(cat) ? categoryMap[cat] : fallbackId++;
                    AvailableCategories.Add(new SelectableCategory
                    {
                        Id = id,
                        Name = cat,
                        BackgroundColor = POS_UI.Helpers.ColorPalette.GetBackgroundColor(cat)
                    });
                }
            }

            if (products != null)
            {
                foreach (var product in products)
                {
                    AvailableItems.Add(new SelectableItem
                    {
                        Id = product.Id,
                        Name = product.ItemName,
                        Category = product.Category,
                        BackgroundColor = POS_UI.Helpers.ColorPalette.GetBackgroundColor(product.Id)
                    });
                }
            }

            LoadExistingSlots(tab, categoryMap, products);
            EnsureEmptySlots();

            FilteredItems = new ObservableCollection<SelectableItem>(AvailableItems);
            FilteredCategories = new ObservableCollection<SelectableCategory>(AvailableCategories);

            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(Cancel);
            ToggleCategoriesCommand = new RelayCommand(() => { IsShowCategories = true; });
            ToggleItemsCommand = new RelayCommand(() => { IsShowItems = true; });
            AssignCategoryCommand = new RelayCommand<SelectableCategory>(AssignCategory);
            AssignItemCommand = new RelayCommand<SelectableItem>(AssignItem);
            ActivateSlotCommand = new RelayCommand<MenuGridSlot>(ActivateSlot);
            RemoveSlotCommand = new RelayCommand<MenuGridSlot>(RemoveSlot);
            ClearAllCommand = new RelayCommand(ClearAll);
            ToggleColorPickerCommand = new RelayCommand<MenuGridSlot>(ToggleColorPicker);
            ChangeSlotColorCommand = new RelayCommand<object>(ChangeSlotColor);
        }

        private void LoadExistingSlots(MenuTabModel tab, Dictionary<string, int> categoryMap, ObservableCollection<ProductItemModel> products)
        {
            if (tab == null) return;

            var idToCategory = categoryMap.ToDictionary(kv => kv.Value, kv => kv.Key);

            if (tab.Slots != null && tab.Slots.Count > 0)
            {
                foreach (var slot in tab.Slots)
                {
                    if (slot.Type == "category")
                    {
                        var name = idToCategory.ContainsKey(slot.Id) ? idToCategory[slot.Id] : $"Category #{slot.Id}";
                        AddFilledSlot("category", slot.Id, name, "", POS_UI.Helpers.ColorPalette.GetBackgroundColor(name));
                    }
                    else if (slot.Type == "item")
                    {
                        var product = products?.FirstOrDefault(p => p.Id == slot.Id);
                        var name = product?.ItemName ?? $"Item #{slot.Id}";
                        var category = product?.Category ?? "";
                        AddFilledSlot("item", slot.Id, name, category, POS_UI.Helpers.ColorPalette.GetBackgroundColor(slot.Id));
                    }
                }
                return;
            }

            if (tab.ContentType == "categories" && tab.CategoryIds != null && tab.CategoryIds.Count > 0)
            {
                foreach (var catId in tab.CategoryIds)
                {
                    var name = idToCategory.ContainsKey(catId) ? idToCategory[catId] : $"Category #{catId}";
                    AddFilledSlot("category", catId, name, "", POS_UI.Helpers.ColorPalette.GetBackgroundColor(name));
                }
            }
            else if (tab.ContentType == "items" && tab.ItemIds != null && tab.ItemIds.Count > 0)
            {
                foreach (var itemId in tab.ItemIds)
                {
                    var product = products?.FirstOrDefault(p => p.Id == itemId);
                    var name = product?.ItemName ?? $"Item #{itemId}";
                    var category = product?.Category ?? "";
                    AddFilledSlot("item", itemId, name, category, POS_UI.Helpers.ColorPalette.GetBackgroundColor(itemId));
                }
            }
        }

        private void AddFilledSlot(string type, int id, string name, string category, string bgColor)
        {
            var slot = new MenuGridSlot
            {
                SlotIndex = MenuSlots.Count,
                IsEmpty = false,
                SlotType = type,
                ItemId = id,
                Name = name,
                Category = category,
                BackgroundColor = bgColor
            };
            MenuSlots.Add(slot);
            MarkPickerItemAsPlaced(type, id, true);
        }

        private void EnsureEmptySlots()
        {
            int emptyCount = MenuSlots.Count(s => s.IsEmpty);

            int needed = MIN_EMPTY_SLOTS - emptyCount;
            for (int i = 0; i < needed; i++)
            {
                MenuSlots.Add(new MenuGridSlot
                {
                    SlotIndex = MenuSlots.Count,
                    IsEmpty = true
                });
            }

            TrimTrailingEmpties();
            ReindexSlots();
        }

        /// <summary>
        /// Keep at most MIN_EMPTY_SLOTS consecutive empty slots at the end.
        /// </summary>
        private void TrimTrailingEmpties()
        {
            int trailingCount = 0;
            for (int i = MenuSlots.Count - 1; i >= 0; i--)
            {
                if (MenuSlots[i].IsEmpty)
                    trailingCount++;
                else
                    break;
            }

            int excess = trailingCount - MIN_EMPTY_SLOTS;
            for (int i = 0; i < excess; i++)
            {
                MenuSlots.RemoveAt(MenuSlots.Count - 1);
            }
        }

        private void ReindexSlots()
        {
            for (int i = 0; i < MenuSlots.Count; i++)
            {
                MenuSlots[i].SlotIndex = i;
            }
        }

        private void UpdateSlotActiveStates()
        {
            for (int i = 0; i < MenuSlots.Count; i++)
            {
                MenuSlots[i].IsActive = (i == _activeSlotIndex && MenuSlots[i].IsEmpty);
            }
        }

        private void MarkPickerItemAsPlaced(string type, int id, bool isPlaced)
        {
            if (type == "category")
            {
                var cat = AvailableCategories.FirstOrDefault(c => c.Id == id);
                if (cat != null) cat.IsPlaced = isPlaced;
            }
            else
            {
                var item = AvailableItems.FirstOrDefault(i => i.Id == id);
                if (item != null) item.IsPlaced = isPlaced;
            }
        }

        private void RefreshAllPlacedStates()
        {
            var placedCategoryIds = new HashSet<int>(MenuSlots.Where(s => !s.IsEmpty && s.SlotType == "category").Select(s => s.ItemId));
            var placedItemIds = new HashSet<int>(MenuSlots.Where(s => !s.IsEmpty && s.SlotType == "item").Select(s => s.ItemId));

            foreach (var cat in AvailableCategories)
                cat.IsPlaced = placedCategoryIds.Contains(cat.Id);
            foreach (var item in AvailableItems)
                item.IsPlaced = placedItemIds.Contains(item.Id);
        }

        private void ActivateSlot(MenuGridSlot slot)
        {
            if (slot == null) return;

            if (slot.IsEmpty)
            {
                ActiveSlotIndex = (ActiveSlotIndex == slot.SlotIndex) ? -1 : slot.SlotIndex;
            }
        }

        private void AssignCategory(SelectableCategory category)
        {
            if (category == null || category.IsPlaced) return;

            if (MenuSlots.Any(s => !s.IsEmpty && s.SlotType == "category" && s.ItemId == category.Id))
                return;

            if (ActiveSlotIndex >= 0 && ActiveSlotIndex < MenuSlots.Count && MenuSlots[ActiveSlotIndex].IsEmpty)
            {
                var slot = MenuSlots[ActiveSlotIndex];
                slot.IsEmpty = false;
                slot.SlotType = "category";
                slot.ItemId = category.Id;
                slot.Name = category.Name;
                slot.Category = "";
                slot.BackgroundColor = category.BackgroundColor;
                slot.IsActive = false;

                ActiveSlotIndex = -1;
            }
            else
            {
                int insertIndex = FindFirstEmptyIndex();
                if (insertIndex >= 0)
                {
                    var slot = MenuSlots[insertIndex];
                    slot.IsEmpty = false;
                    slot.SlotType = "category";
                    slot.ItemId = category.Id;
                    slot.Name = category.Name;
                    slot.Category = "";
                    slot.BackgroundColor = category.BackgroundColor;
                }
                else
                {
                    AddFilledSlot("category", category.Id, category.Name, "", category.BackgroundColor);
                }
            }

            MarkPickerItemAsPlaced("category", category.Id, true);
            EnsureEmptySlots();
            NotifyPlacedChanged();
        }

        private void AssignItem(SelectableItem item)
        {
            if (item == null || item.IsPlaced) return;

            if (MenuSlots.Any(s => !s.IsEmpty && s.SlotType == "item" && s.ItemId == item.Id))
                return;

            if (ActiveSlotIndex >= 0 && ActiveSlotIndex < MenuSlots.Count && MenuSlots[ActiveSlotIndex].IsEmpty)
            {
                var slot = MenuSlots[ActiveSlotIndex];
                slot.IsEmpty = false;
                slot.SlotType = "item";
                slot.ItemId = item.Id;
                slot.Name = item.Name;
                slot.Category = item.Category;
                slot.BackgroundColor = item.BackgroundColor;
                slot.IsActive = false;

                ActiveSlotIndex = -1;
            }
            else
            {
                int insertIndex = FindFirstEmptyIndex();
                if (insertIndex >= 0)
                {
                    var slot = MenuSlots[insertIndex];
                    slot.IsEmpty = false;
                    slot.SlotType = "item";
                    slot.ItemId = item.Id;
                    slot.Name = item.Name;
                    slot.Category = item.Category;
                    slot.BackgroundColor = item.BackgroundColor;
                }
                else
                {
                    AddFilledSlot("item", item.Id, item.Name, item.Category, item.BackgroundColor);
                }
            }

            MarkPickerItemAsPlaced("item", item.Id, true);
            EnsureEmptySlots();
            NotifyPlacedChanged();
        }

        /// <summary>
        /// Find the first empty slot index, or -1 if none.
        /// </summary>
        private int FindFirstEmptyIndex()
        {
            for (int i = 0; i < MenuSlots.Count; i++)
            {
                if (MenuSlots[i].IsEmpty) return i;
            }
            return -1;
        }

        /// <summary>
        /// Removes an item from a slot by clearing it back to empty.
        /// The slot stays in place so other items don't shift positions.
        /// </summary>
        private void RemoveSlot(MenuGridSlot slot)
        {
            if (slot == null || slot.IsEmpty) return;

            MarkPickerItemAsPlaced(slot.SlotType, slot.ItemId, false);

            slot.IsEmpty = true;
            slot.SlotType = "";
            slot.ItemId = 0;
            slot.Name = "";
            slot.Category = "";
            slot.BackgroundColor = "Transparent";
            slot.IsActive = false;

            TrimTrailingEmpties();
            EnsureEmptySlots();
            NotifyPlacedChanged();
        }

        private void ToggleColorPicker(MenuGridSlot slot)
        {
            if (slot == null || slot.IsEmpty) return;

            // Close any other open pickers
            foreach (var s in MenuSlots)
            {
                if (s != slot) s.IsColorPickerOpen = false;
            }

            slot.IsColorPickerOpen = !slot.IsColorPickerOpen;
        }

        private void ChangeSlotColor(object parameter)
        {
            if (parameter is object[] args && args.Length == 2 && args[0] is MenuGridSlot slot && args[1] is string color)
            {
                slot.BackgroundColor = color;
                slot.IsColorPickerOpen = false;

                // Persist the color choice
                if (slot.SlotType == "category")
                    POS_UI.Helpers.ColorPalette.SetCategoryColor(slot.Name, POS_UI.Helpers.ColorPalette.GetAllColors().IndexOf(color));
                else if (slot.SlotType == "item")
                    POS_UI.Helpers.ColorPalette.SetProductColor(slot.ItemId, POS_UI.Helpers.ColorPalette.GetAllColors().IndexOf(color));

                // Also update the picker panel item's color
                if (slot.SlotType == "category")
                {
                    var cat = AvailableCategories.FirstOrDefault(c => c.Id == slot.ItemId);
                    if (cat != null) cat.BackgroundColor = color;
                }
                else
                {
                    var item = AvailableItems.FirstOrDefault(i => i.Id == slot.ItemId);
                    if (item != null) item.BackgroundColor = color;
                }

                // Refresh filtered lists to show updated colors
                FilterCategories();
                FilterItems();
            }
        }

        private void ClearAll()
        {
            MenuSlots.Clear();
            foreach (var cat in AvailableCategories) cat.IsPlaced = false;
            foreach (var item in AvailableItems) item.IsPlaced = false;
            EnsureEmptySlots();
            NotifyPlacedChanged();
        }

        private void NotifyPlacedChanged()
        {
            OnPropertyChanged(nameof(PlacedCount));
            OnPropertyChanged(nameof(HasPlacedItems));
            OnPropertyChanged(nameof(PlacedLabel));
        }

        private void FilterCategories()
        {
            if (string.IsNullOrWhiteSpace(CategorySearchText))
                FilteredCategories = new ObservableCollection<SelectableCategory>(AvailableCategories);
            else
                FilteredCategories = new ObservableCollection<SelectableCategory>(
                    AvailableCategories.Where(c => c.Name.Contains(CategorySearchText, StringComparison.OrdinalIgnoreCase)));
        }

        private void FilterItems()
        {
            if (string.IsNullOrWhiteSpace(ItemSearchText))
                FilteredItems = new ObservableCollection<SelectableItem>(AvailableItems);
            else
                FilteredItems = new ObservableCollection<SelectableItem>(
                    AvailableItems.Where(i =>
                        i.Name.Contains(ItemSearchText, StringComparison.OrdinalIgnoreCase) ||
                        (i.Category != null && i.Category.Contains(ItemSearchText, StringComparison.OrdinalIgnoreCase))));
        }

        private async void Save()
        {
            if (string.IsNullOrWhiteSpace(TabName))
            {
                System.Windows.MessageBox.Show("Please enter a tab name.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var filledSlots = MenuSlots.Where(s => !s.IsEmpty).ToList();
            if (filledSlots.Count == 0)
            {
                System.Windows.MessageBox.Show("Please add at least one category or item to the menu.", "Validation Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var tab = _originalTab ?? new MenuTabModel();
            tab.Name = TabName.Trim();

            tab.Slots = filledSlots.Select(s => new MenuSlotEntry { Type = s.SlotType, Id = s.ItemId }).ToList();

            tab.CategoryIds = filledSlots.Where(s => s.SlotType == "category").Select(s => s.ItemId).ToList();
            tab.ItemIds = filledSlots.Where(s => s.SlotType == "item").Select(s => s.ItemId).ToList();

            bool hasCategories = tab.CategoryIds.Count > 0;
            bool hasItems = tab.ItemIds.Count > 0;

            if (hasCategories && hasItems)
                tab.ContentType = "mixed";
            else if (hasItems)
                tab.ContentType = "items";
            else
                tab.ContentType = "categories";

            System.Diagnostics.Debug.WriteLine($"[EditMenuTab] Saving tab '{tab.Name}' as {tab.ContentType}: {filledSlots.Count} slots ({tab.CategoryIds.Count} categories, {tab.ItemIds.Count} items)");

            if (OnSave != null)
                await OnSave(tab);
        }

        private void Cancel()
        {
            OnCancel?.Invoke();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a single slot in the menu grid layout (filled or empty).
    /// </summary>
    public class MenuGridSlot : INotifyPropertyChanged
    {
        private int _slotIndex;
        private bool _isEmpty = true;
        private bool _isActive;
        private string _slotType = "";
        private int _itemId;
        private string _name = "";
        private string _category = "";
        private string _backgroundColor = "Transparent";

        /// <summary>0-based position in the collection.</summary>
        public int SlotIndex
        {
            get => _slotIndex;
            set { _slotIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayNumber)); }
        }

        /// <summary>1-based display number shown on the UI.</summary>
        public int DisplayNumber => SlotIndex + 1;

        public bool IsEmpty
        {
            get => _isEmpty;
            set { _isEmpty = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsFilled)); }
        }

        public bool IsFilled => !IsEmpty;

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isColorPickerOpen;
        public bool IsColorPickerOpen
        {
            get => _isColorPickerOpen;
            set { _isColorPickerOpen = value; OnPropertyChanged(); }
        }

        public string SlotType
        {
            get => _slotType;
            set { _slotType = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeLabel)); OnPropertyChanged(nameof(TypeColor)); }
        }

        public string TypeLabel => SlotType == "category" ? "CAT" : "ITEM";
        public string TypeColor => SlotType == "category" ? "#4CAF50" : "#FF9800";

        public int ItemId
        {
            get => _itemId;
            set { _itemId = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectableCategory : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isPlaced;
        private string _backgroundColor = "#4CAF50";
        private int _displayOrder = 0;

        public int Id { get; set; }
        public string Name { get; set; }
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); }
        }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public bool IsPlaced
        {
            get => _isPlaced;
            set { _isPlaced = value; OnPropertyChanged(); }
        }

        public int DisplayOrder
        {
            get => _displayOrder;
            set { _displayOrder = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayOrderText)); }
        }

        public string DisplayOrderText => DisplayOrder > 0 ? DisplayOrder.ToString() : "";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectableItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isPlaced;
        private string _backgroundColor = "#FF9800";
        private int _displayOrder = 0;

        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string BackgroundColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; OnPropertyChanged(); }
        }
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public bool IsPlaced
        {
            get => _isPlaced;
            set { _isPlaced = value; OnPropertyChanged(); }
        }

        public int DisplayOrder
        {
            get => _displayOrder;
            set { _displayOrder = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayOrderText)); }
        }

        public string DisplayOrderText => DisplayOrder > 0 ? DisplayOrder.ToString() : "";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
