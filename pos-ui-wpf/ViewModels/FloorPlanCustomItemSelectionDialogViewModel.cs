using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MaterialDesignThemes.Wpf;
using POS_UI.Models;

namespace POS_UI.ViewModels
{
    /// <summary>Add Floor Item: each tile (shape or catalog type) places on the canvas immediately when tapped.</summary>
    public sealed class FloorPlanCustomItemSelectionDialogViewModel : BaseViewModel
    {
        public ObservableCollection<FloorPlanCustomItemTypeModel> Types { get; }

        public ObservableCollection<FloorPlanShapeType> ShapeOptions { get; } = new ObservableCollection<FloorPlanShapeType>
        {
            FloorPlanShapeType.Rectangle,
            FloorPlanShapeType.Square,
            FloorPlanShapeType.Circle,
            FloorPlanShapeType.Oval,
            FloorPlanShapeType.Pill,
            FloorPlanShapeType.Rounded,
            FloorPlanShapeType.Parallelogram,
            FloorPlanShapeType.Diamond
        };

        public bool HasCatalogTypes => Types.Count > 0;

        public RelayCommand<FloorPlanCustomItemTypeModel> PickTypeCommand { get; }
        public RelayCommand<FloorPlanShapeType> PlaceShapePrimitiveCommand { get; }
        public RelayCommand CancelCommand { get; }

        public FloorPlanCustomItemSelectionDialogViewModel(IEnumerable<FloorPlanCustomItemTypeModel> types)
        {
            Types = new ObservableCollection<FloorPlanCustomItemTypeModel>(
                types.OrderBy(t => t.DisplayName, System.StringComparer.OrdinalIgnoreCase));
            PickTypeCommand = new RelayCommand<FloorPlanCustomItemTypeModel>(PickCatalogType, t => t != null);
            PlaceShapePrimitiveCommand = new RelayCommand<FloorPlanShapeType>(PlaceShapePrimitive, _ => true);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void PlaceShapePrimitive(FloorPlanShapeType shape)
        {
            var result = new FloorPlanAddFloorItemPickResult
            {
                ShapePrimitive = shape
            };

            DialogHost.CloseDialogCommand.Execute(result, null);
        }

        private void PickCatalogType(FloorPlanCustomItemTypeModel? type)
        {
            if (type == null)
            {
                return;
            }

            var result = new FloorPlanAddFloorItemPickResult
            {
                CatalogType = type
            };

            DialogHost.CloseDialogCommand.Execute(result, null);
        }

        private void Cancel() => DialogHost.CloseDialogCommand.Execute(null, null);
    }
}
