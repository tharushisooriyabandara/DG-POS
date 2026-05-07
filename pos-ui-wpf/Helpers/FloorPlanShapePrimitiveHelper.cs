using POS_UI.Models;

namespace POS_UI.Helpers
{
    /// <summary>Reserved catalog key and defaults for shape-only floor items (Add Floor Item → shape tile).</summary>
    public static class FloorPlanShapePrimitiveHelper
    {
        /// <summary>Persisted <see cref="FloorPlanTablePlacementModel.ItemTypeKey"/> for primitive shape tiles.</summary>
        public const string ItemTypeKey = "_primitive.shape";

        public static string DisplayLabel(FloorPlanShapeType shape) => shape.ToString();

        /// <summary>Icons are best-effort strings for <see cref="Converters.PackIconKindConverter"/>.</summary>
        public static string IconKindName(FloorPlanShapeType shape) =>
            shape switch
            {
                FloorPlanShapeType.Circle or FloorPlanShapeType.Oval => "CircleOutline",
                FloorPlanShapeType.Square or FloorPlanShapeType.Diamond => "SquareOutline",
                _ => "RectangleOutline"
            };

        public static (double Width, double Height) DefaultSize(FloorPlanShapeType shape) =>
            shape switch
            {
                FloorPlanShapeType.Square or FloorPlanShapeType.Circle or FloorPlanShapeType.Diamond => (88, 88),
                FloorPlanShapeType.Oval => (120, 72),
                FloorPlanShapeType.Pill => (128, 56),
                FloorPlanShapeType.Parallelogram => (110, 72),
                FloorPlanShapeType.Rounded => (100, 72),
                _ => (100, 72)
            };
    }
}
