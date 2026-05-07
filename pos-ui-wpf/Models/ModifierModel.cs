using System.ComponentModel;
using System.Collections.Generic;

namespace POS_UI.Models
{
    public class ModifierModel : INotifyPropertyChanged
    {
        private int _id;
        private string _title;
        private List<ModifierItemModel> _modifierItems;
        private bool _isSelected;
        private int _minPermitted;
        private int _maxPermitted;
        private int _defaultQuantity;
        private bool _isTaxInherited;
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public List<ModifierItemModel> ModifierItems
        {
            get => _modifierItems;
            set { _modifierItems = value; OnPropertyChanged(nameof(ModifierItems)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public int MinPermitted
        {
            get => _minPermitted;
            set { _minPermitted = value; OnPropertyChanged(nameof(MinPermitted)); }
        }

        public int MaxPermitted
        {
            get => _maxPermitted;
            set { _maxPermitted = value; OnPropertyChanged(nameof(MaxPermitted)); }
        }

        public int DefaultQuantity
        {
            get => _defaultQuantity;
            set { _defaultQuantity = value; OnPropertyChanged(nameof(DefaultQuantity)); }
        }

        public string ConstraintText
        {
            get
            {
                if (MinPermitted == 1 && MaxPermitted == 1)
                    return "Required, Only select One";
                if (MaxPermitted > 1)
                    return $"choose up to {MaxPermitted}";
                if (MinPermitted == 1)
                    return "Required";
                if (MaxPermitted == 1)
                    return "Only select One";
                return string.Empty;
            }
        }

        public string MinConstraintText
        {
            get
            {
                if (MinPermitted == 1)
                    return "Required";
                if (MinPermitted > 1)
                    return $"Minimum required: {MinPermitted}";
                return string.Empty;
            }
        }

        public string MaxConstraintText
        {
            get
            {
                if (MaxPermitted == 1)
                    return "Only select One";
                if (MaxPermitted > 1)
                    return $"choose up to {MaxPermitted}";
                return string.Empty;
            }
        }

        public string SelectionStatusText
        {
            get
            {
                if (ModifierItems == null) return string.Empty;
                
                var selectedCount = ModifierItems.Count(item => item.IsSelected);
                
                if (MinPermitted > 0 && selectedCount < MinPermitted)
                {
                    return $"Please select at least {MinPermitted} item(s)";
                }
                
                if (MaxPermitted > 0 && selectedCount >= MaxPermitted)
                {
                    return $"Maximum {MaxPermitted} item(s) selected";
                }
                
                if (MaxPermitted > 1)
                {
                    return $"Selected: {selectedCount}/{MaxPermitted}";
                }
                
                return string.Empty;
            }
        }

        public bool IsTaxInherited
        {
            get => _isTaxInherited;
            set { _isTaxInherited = value; OnPropertyChanged(nameof(IsTaxInherited)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ModifierItemModel : INotifyPropertyChanged
    {
        private int _id;
        private string _itemName;
        private decimal _itemPrice;
        private decimal _originalPrice;
        private bool _isSelected;
        private List<ModifierModel> _nestedModifiers;
        private string _externalItemId;
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(nameof(Id)); }
        }

        public string ItemName
        {
            get => _itemName;
            set { _itemName = value; OnPropertyChanged(nameof(ItemName)); }
        }

        public decimal ItemPrice
        {
            get => _itemPrice;
            set { _itemPrice = value; OnPropertyChanged(nameof(ItemPrice)); }
        }

        public decimal OriginalPrice
        {
            get => _originalPrice;
            set { _originalPrice = value; OnPropertyChanged(nameof(OriginalPrice)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public List<ModifierModel> NestedModifiers
        {
            get => _nestedModifiers;
            set { _nestedModifiers = value; OnPropertyChanged(nameof(NestedModifiers)); }
        }

        public bool HasNestedModifiers => NestedModifiers != null && NestedModifiers.Count > 0;

        public string ExternalItemId
        {
            get => _externalItemId;
            set { _externalItemId = value; OnPropertyChanged(nameof(ExternalItemId)); }
        }

        public int? TaxProfileId { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 