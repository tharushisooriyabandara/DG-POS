using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace POS_UI.Models
{
    /// <summary>Definition of a placeable floor-plan decoration (from API <c>floorPlanCustomItemTypes</c> merged with built-in defaults).</summary>
    public sealed class FloorPlanCustomItemTypeModel : INotifyPropertyChanged
    {
        private string _key = string.Empty;
        private string _displayName = string.Empty;
        private string _iconKind = "MapMarker";
        private string _defaultColorHex = "#78909C";
        private int _defaultWidth = 100;
        private int _defaultHeight = 72;
        private FloorPlanShapeType _defaultShape = FloorPlanShapeType.Rectangle;

        [JsonPropertyName("key")]
        public string Key
        {
            get => _key;
            set { _key = value ?? string.Empty; OnPropertyChanged(); }
        }

        [JsonPropertyName("displayName")]
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>MaterialDesign <see cref="MaterialDesignThemes.Wpf.PackIconKind"/> name.</summary>
        [JsonPropertyName("iconKind")]
        public string IconKind
        {
            get => _iconKind;
            set { _iconKind = string.IsNullOrWhiteSpace(value) ? "MapMarker" : value!; OnPropertyChanged(); }
        }

        [JsonPropertyName("defaultColorHex")]
        public string DefaultColorHex
        {
            get => _defaultColorHex;
            set { _defaultColorHex = string.IsNullOrWhiteSpace(value) ? "#78909C" : value!; OnPropertyChanged(); }
        }

        [JsonPropertyName("defaultWidth")]
        public int DefaultWidth
        {
            get => _defaultWidth;
            set { _defaultWidth = value < 40 ? 40 : value; OnPropertyChanged(); }
        }

        [JsonPropertyName("defaultHeight")]
        public int DefaultHeight
        {
            get => _defaultHeight;
            set { _defaultHeight = value < 40 ? 40 : value; OnPropertyChanged(); }
        }

        /// <summary>Shape applied when a new instance of this type is placed (JSON: lowercase enum name, e.g. <c>pill</c>).</summary>
        [JsonIgnore]
        public FloorPlanShapeType DefaultShape
        {
            get => _defaultShape;
            set
            {
                if (_defaultShape == value)
                {
                    return;
                }

                _defaultShape = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DefaultShapeSerialized));
            }
        }

        [JsonPropertyName("defaultShape")]
        public string? DefaultShapeSerialized
        {
            get => _defaultShape.ToString().ToLowerInvariant();
            set
            {
                var parsed = ParseDefaultShape(value);
                if (_defaultShape == parsed)
                {
                    return;
                }

                _defaultShape = parsed;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DefaultShape));
                OnPropertyChanged(nameof(DefaultShapeSerialized));
            }
        }

        public FloorPlanCustomItemTypeModel Clone() => new()
        {
            Key = Key,
            DisplayName = DisplayName,
            IconKind = IconKind,
            DefaultColorHex = DefaultColorHex,
            DefaultWidth = DefaultWidth,
            DefaultHeight = DefaultHeight,
            DefaultShape = _defaultShape
        };

        private static FloorPlanShapeType ParseDefaultShape(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return FloorPlanShapeType.Rectangle;
            }

            return Enum.TryParse(s.Trim(), ignoreCase: true, out FloorPlanShapeType shape)
                ? shape
                : FloorPlanShapeType.Rectangle;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
