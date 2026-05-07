using System;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Models;

namespace POS_UI.Helpers
{
    /// <summary>Built-in floor-plan decoration types (restaurant domain). API entries with the same <see cref="FloorPlanCustomItemTypeModel.Key"/> override these.</summary>
    public static class FloorPlanCustomItemCatalog
    {
        public static IReadOnlyList<FloorPlanCustomItemTypeModel> GetBuiltInTypes() => BuiltIn;

        private static readonly FloorPlanCustomItemTypeModel[] BuiltIn =
        {
            New("bar", "Bar", "Beer", "#5C6BC0", 110, 72),
            New("kitchen", "Kitchen", "PotSteamOutline", "#FF7043", 120, 80),
            New("restroom", "Restroom", "HumanMaleFemale", "#26A69A", 72, 72),
            New("entrance", "Entrance", "Door", "#8D6E63", 90, 64),
            New("waiting_area", "Waiting area", "ChairSchool", "#9575CD", 130, 76),
            New("counter", "Counter / POS", "Monitor", "#546E7A", 100, 56),
            New("buffet", "Buffet line", "Food", "#FFA726", 140, 70),
            New("dj_stage", "DJ / Stage", "MusicNote", "#EC407A", 96, 72),
            New("outdoor", "Outdoor / Patio", "Tree", "#66BB6A", 100, 72),
            New("stairs", "Stairs", "Stairs", "#78909C", 64, 96),
            New("elevator", "Elevator", "Elevator", "#90A4AE", 72, 80),
            New("storage", "Storage", "Archive", "#A1887F", 88, 64),
            New("host_stand", "Host stand", "Podium", "#42A5F5", 80, 60),
            New("high_tops", "High-top row", "TableFurniture", "#7E57C2", 120, 56),
            New("booth_block", "Booth seating", "SofaSingleOutline", "#8E24AA", 130, 68),
            New("salad_bar", "Salad bar", "Carrot", "#43A047", 110, 64),
            New("grill", "Grill station", "Fire", "#D84315", 96, 72),
            New("dessert", "Dessert station", "IceCream", "#F06292", 88, 64),
            New("wine", "Wine / cellar", "GlassWine", "#880E4F", 72, 80),
            New("office", "Office", "Briefcase", "#607D8B", 88, 64),
            New("wall", "Wall / partition", "RectangleOutline", "#B0BEC5", 140, 40),
            New("cash_register", "Cash / till", "CashRegister", "#37474F", 88, 56),
            New("coffee", "Coffee station", "Coffee", "#6D4C41", 88, 64),
            New("handicap", "Accessible", "HumanHandicap", "#039BE5", 72, 72),
            New("exit", "Emergency exit", "ExitToApp", "#C62828", 80, 64)
        };

        private static FloorPlanCustomItemTypeModel New(string key, string display, string iconKind, string color, int w, int h, FloorPlanShapeType defaultShape = FloorPlanShapeType.Rectangle) => new()
        {
            Key = key,
            DisplayName = display,
            IconKind = iconKind,
            DefaultColorHex = color,
            DefaultWidth = w,
            DefaultHeight = h,
            DefaultShape = defaultShape
        };

        /// <summary>Built-in list first; API types with matching <paramref name="apiTypes"/> keys replace entries.</summary>
        public static List<FloorPlanCustomItemTypeModel> MergeWithApi(IEnumerable<FloorPlanCustomItemTypeModel>? apiTypes)
        {
            var map = BuiltIn.Select(b => b.Clone()).ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
            if (apiTypes != null)
            {
                foreach (var t in apiTypes)
                {
                    if (string.IsNullOrWhiteSpace(t.Key))
                    {
                        continue;
                    }

                    map[t.Key] = t.Clone();
                }
            }

            return map.Values.OrderBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
}
