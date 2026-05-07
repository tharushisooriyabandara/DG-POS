namespace POS_UI.Models
{
    public enum FloorPlanShapeType
    {
        Rectangle,
        Square,
        Circle,
        Oval,
        /// <summary>Stadium / capsule: fully rounded short sides.</summary>
        Pill,
        /// <summary>Soft rectangle with large corner radius.</summary>
        Rounded,
        /// <summary>Slanted rectangle (skew).</summary>
        Parallelogram,
        /// <summary>Square rotated 45° (width/height stay equal).</summary>
        Diamond
    }
}
