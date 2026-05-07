namespace POS_UI.Models
{
    /// <summary>Outcome of Add Floor Item: place a catalog element, or place a bare shape tile (same as immediate canvas add).</summary>
    public sealed class FloorPlanAddFloorItemPickResult
    {
        /// <summary>When set, place merged catalog custom item (shape comes from <see cref="FloorPlanCustomItemTypeModel.DefaultShape"/> only).</summary>
        public FloorPlanCustomItemTypeModel? CatalogType { get; init; }

        /// <summary>When set (and <see cref="CatalogType"/> is null), place a generic shape decoration on the canvas.</summary>
        public FloorPlanShapeType? ShapePrimitive { get; init; }
    }
}
