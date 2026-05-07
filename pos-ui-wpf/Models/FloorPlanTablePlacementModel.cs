using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class FloorPlanTablePlacementModel : INotifyPropertyChanged
    {
        private FloorPlanElementKind _kind = FloorPlanElementKind.Table;

        /// <summary>Table vs. decorative / zone marker (bar, kitchen, etc.).</summary>
        public FloorPlanElementKind Kind
        {
            get => _kind;
            set
            {
                if (_kind == value) return;
                _kind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTable));
                OnPropertyChanged(nameof(IsCustomItem));
            }
        }

        public bool IsTable => Kind == FloorPlanElementKind.Table;
        public bool IsCustomItem => Kind == FloorPlanElementKind.CustomItem;

        /// <summary>Stable id for <see cref="FloorPlanElementKind.CustomItem"/> (persisted to API).</summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>Catalog key for custom items (e.g. <c>bar</c>, <c>kitchen</c>).</summary>
        public string ItemTypeKey { get; set; } = string.Empty;

        /// <summary>MaterialDesign <c>PackIconKind</c> name for custom items.</summary>
        public string IconKindName { get; set; } = string.Empty;

        public int TableId { get; set; }
        public string TableName { get; set; } = string.Empty;
        public int SeatCount { get; set; }

        private double _x = 40;
        public double X
        {
            get => _x;
            set
            {
                _x = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        private double _y = 40;
        public double Y
        {
            get => _y;
            set
            {
                _y = value < 0 ? 0 : value;
                OnPropertyChanged();
            }
        }

        private double _width = 120;
        public double Width
        {
            get => _width;
            set
            {
                _width = value < 50 ? 50 : value;
                if (_shape == FloorPlanShapeType.Square || _shape == FloorPlanShapeType.Circle || _shape == FloorPlanShapeType.Diamond)
                {
                    _height = _width;
                    OnPropertyChanged(nameof(Height));
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(CornerRadiusValue));
            }
        }

        private double _height = 80;
        public double Height
        {
            get => _height;
            set
            {
                _height = value < 50 ? 50 : value;
                if (_shape == FloorPlanShapeType.Square || _shape == FloorPlanShapeType.Circle || _shape == FloorPlanShapeType.Diamond)
                {
                    _width = _height;
                    OnPropertyChanged(nameof(Width));
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(CornerRadiusValue));
            }
        }

        private FloorPlanShapeType _shape = FloorPlanShapeType.Rectangle;
        public FloorPlanShapeType Shape
        {
            get => _shape;
            set
            {
                _shape = value;
                if (_shape == FloorPlanShapeType.Square || _shape == FloorPlanShapeType.Circle || _shape == FloorPlanShapeType.Diamond)
                {
                    Height = Width;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(CornerRadiusValue));
            }
        }

        private string _colorHex = "#4CAF50";
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                _colorHex = string.IsNullOrWhiteSpace(value) ? "#4CAF50" : value;
                OnPropertyChanged();
            }
        }

        private bool _isSelectedOnCanvas;
        public bool IsSelectedOnCanvas
        {
            get => _isSelectedOnCanvas;
            set
            {
                if (_isSelectedOnCanvas == value) return;
                _isSelectedOnCanvas = value;
                OnPropertyChanged();
            }
        }

        public double CornerRadiusValue
        {
            get
            {
                return Shape switch
                {
                    FloorPlanShapeType.Circle => 999,
                    FloorPlanShapeType.Oval => 999,
                    FloorPlanShapeType.Pill => Math.Min(Width, Height) / 2.0,
                    FloorPlanShapeType.Rounded => Math.Min(48, Math.Max(12, Math.Min(Width, Height) * 0.22)),
                    FloorPlanShapeType.Parallelogram => 6,
                    FloorPlanShapeType.Diamond => 2,
                    _ => 8
                };
            }
        }

        public FloorPlanTablePlacementModel Clone()
        {
            return new FloorPlanTablePlacementModel
            {
                Kind = Kind,
                InstanceId = InstanceId,
                ItemTypeKey = ItemTypeKey,
                IconKindName = IconKindName,
                TableId = TableId,
                TableName = TableName,
                SeatCount = SeatCount,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                Shape = Shape,
                ColorHex = ColorHex,
                IsSelectedOnCanvas = false
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
