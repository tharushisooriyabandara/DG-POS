using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    /// <summary>
    /// Unified display model for mixed menu tabs.
    /// Can represent either a category or an item in the same grid.
    /// </summary>
    public class MenuDisplayItem : INotifyPropertyChanged
    {
        private string _displayType;
        private string _name;
        private string _secondLine;
        private string _backgroundColor;
        private string _textColor;
        private int _categoryId;
        private ProductItemModel _product;

        /// <summary>"category" or "item"</summary>
        public string DisplayType
        {
            get => _displayType;
            set { _displayType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCategory)); OnPropertyChanged(nameof(IsItem)); }
        }

        public bool IsCategory => DisplayType == "category";
        public bool IsItem => DisplayType == "item";

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        /// <summary>For categories: "X items". For items: formatted price.</summary>
        public string SecondLine
        {
            get => _secondLine;
            set { _secondLine = value; OnPropertyChanged(); }
        }

        public string BackgroundColor
        {
            get => _backgroundColor ?? "#1976D2";
            set { _backgroundColor = value; OnPropertyChanged(); }
        }

        public string TextColor
        {
            get => _textColor ?? "#FFFFFF";
            set { _textColor = value; OnPropertyChanged(); }
        }

        /// <summary>For categories: the category ID. For items: unused.</summary>
        public int CategoryId
        {
            get => _categoryId;
            set { _categoryId = value; OnPropertyChanged(); }
        }

        /// <summary>For items: the product model (used for add-to-cart). Null for categories.</summary>
        public ProductItemModel Product
        {
            get => _product;
            set { _product = value; OnPropertyChanged(); }
        }

        /// <summary>Category name string for SelectCategoryCommand. Null for items.</summary>
        public string CategoryName { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
