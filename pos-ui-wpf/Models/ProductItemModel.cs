using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace POS_UI.Models
{
    public class ProductItemModel : INotifyPropertyChanged
    {
        private int _id;
        private string _itemName;
        private decimal _price;
        private string _category;

        private string _imageUrl;
        private decimal _pricePerItem;
        private int _categoryId;
        private string _backgroundColor;
        private string _textColor;

        public int Id
        {
            get => _id;
            set 
            { 
                _id = value; 
                OnPropertyChanged(nameof(Id));
                // Auto-assign color based on ID and name for semantic matching
                UpdateColors();
            }
        }

        public string BackgroundColor
        {
            get => _backgroundColor ?? "#1976D2";
            set { _backgroundColor = value; OnPropertyChanged(nameof(BackgroundColor)); }
        }

        public string TextColor
        {
            get => _textColor ?? "#FFFFFF";
            set { _textColor = value; OnPropertyChanged(nameof(TextColor)); }
        }

        private void UpdateColors()
        {
            if (_id > 0)
            {
                // Pass both ID and ItemName for semantic color matching (version 2)
                BackgroundColor = Helpers.ColorPalette.GetBackgroundColor(_id, _itemName);
                TextColor = Helpers.ColorPalette.GetTextColor();
            }
        }

        public string ItemName
        {
            get => _itemName;
            set 
            { 
                _itemName = value; 
                OnPropertyChanged(nameof(ItemName));
                // Update colors when name changes (for semantic matching)
                UpdateColors();
            }
        }

        public decimal Price
        {
            get => _price;
            set { _price = value; OnPropertyChanged(nameof(Price)); }
        }

        public decimal PricePerItem
        {
            get => _pricePerItem;
            set { _pricePerItem = value; OnPropertyChanged(nameof(PricePerItem)); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public int CategoryId
        {
            get => _categoryId;
            set { _categoryId = value; OnPropertyChanged(nameof(CategoryId)); }
        }

        public string ImageUrl
        {
            get => _imageUrl;
            set { _imageUrl = value; OnPropertyChanged(nameof(ImageUrl)); }
        }

        public int? TaxProfileId { get; set; }

        public List<ModifierModel> Modifiers { get; set; } = new List<ModifierModel>();

        public List<PrinterGroupModel> PrinterGroups { get; set; } = new List<PrinterGroupModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 