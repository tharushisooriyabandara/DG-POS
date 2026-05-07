using System.ComponentModel;

namespace POS_UI.Models
{
    public class CategoryModel : INotifyPropertyChanged
    {
        private string _categoryName;
        private int _quantity;
        private string _backgroundColor;
        private string _textColor;

        public string CategoryName
        {
            get => _categoryName;
            set 
            { 
                _categoryName = value; 
                OnPropertyChanged(nameof(CategoryName));
                // Auto-assign color based on category name for consistency
                UpdateColors();
            }
        }

        public int Quantity
        {
            get => _quantity;
            set { _quantity = value; OnPropertyChanged(nameof(Quantity)); }
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
            if (!string.IsNullOrWhiteSpace(_categoryName))
            {
                BackgroundColor = Helpers.ColorPalette.GetBackgroundColor(_categoryName);
                TextColor = Helpers.ColorPalette.GetTextColor();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
} 