using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    /// <summary>
    /// Represents a single slot entry in a mixed menu tab (category or item)
    /// </summary>
    public class MenuSlotEntry
    {
        public string Type { get; set; } // "category" or "item"
        public int Id { get; set; }
    }

    /// <summary>
    /// Represents a single tab in the cashier menu system
    /// </summary>
    public class MenuTabModel : INotifyPropertyChanged
    {
        private int _id;
        private string _name;
        private int _order;
        private bool _isDefault;
        private string _contentType;
        private List<int> _categoryIds;
        private List<int> _itemIds;
        private List<MenuSlotEntry> _slots;
        private bool _isSelected;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int Order
        {
            get => _order;
            set { _order = value; OnPropertyChanged(); }
        }

        public bool IsDefault
        {
            get => _isDefault;
            set { _isDefault = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Content type: "categories" or "items"
        /// </summary>
        public string ContentType
        {
            get => _contentType;
            set { _contentType = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// List of category IDs to show in this tab (empty = all categories)
        /// Only used when ContentType = "categories"
        /// </summary>
        public List<int> CategoryIds
        {
            get => _categoryIds ?? new List<int>();
            set { _categoryIds = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// List of item IDs to show in this tab
        /// Only used when ContentType = "items"
        /// </summary>
        public List<int> ItemIds
        {
            get => _itemIds ?? new List<int>();
            set { _itemIds = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Ordered list of mixed slots (categories and items interleaved).
        /// Used when ContentType = "mixed". Also populated for backward compat on other types.
        /// </summary>
        public List<MenuSlotEntry> Slots
        {
            get => _slots;
            set { _slots = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Indicates if this tab is currently selected in the UI
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
