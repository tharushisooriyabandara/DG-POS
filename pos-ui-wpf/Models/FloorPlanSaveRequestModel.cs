using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace POS_UI.Models
{
    /// <summary>
    /// Payload stored in API <c>data</c> for GET/PATCH /config/floor_plan (camelCase in JSON).
    /// </summary>
    public class FloorPlanConfigDataModel
    {
        public string Name { get; set; } = "Floor plan configuration";

        /// <summary>When true, Cashier dine-in uses designed floor plans for table selection.</summary>
        [JsonPropertyName("floorPlanLayoutEnabled")]
        public bool FloorPlanLayoutEnabled { get; set; }

        [JsonPropertyName("floorPlans")]
        public List<FloorPlanSaveItemModel> FloorPlans { get; set; } = new List<FloorPlanSaveItemModel>();

        /// <summary>Optional catalog of placeable non-table elements; merged client-side with built-in defaults.</summary>
        [JsonPropertyName("floorPlanCustomItemTypes")]
        public List<FloorPlanCustomItemTypeModel> FloorPlanCustomItemTypes { get; set; } = new List<FloorPlanCustomItemTypeModel>();
    }

    public class FloorPlanSaveRequestModel
    {
        public int ShopId { get; set; }
        public int BrandId { get; set; }
        public List<FloorPlanSaveItemModel> FloorPlans { get; set; } = new List<FloorPlanSaveItemModel>();
    }

    public class FloorPlanSaveItemModel
    {
        public int FloorPlanId { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<FloorPlanSaveTableModel> Tables { get; set; } = new List<FloorPlanSaveTableModel>();

        [JsonPropertyName("customItems")]
        public List<FloorPlanSaveCustomItemModel> CustomItems { get; set; } = new List<FloorPlanSaveCustomItemModel>();
    }

    public class FloorPlanSaveCustomItemModel
    {
        public string InstanceId { get; set; } = string.Empty;
        public string ItemTypeKey { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string IconKind { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Shape { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
    }

    public class FloorPlanSaveTableModel
    {
        public int TableId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SeatCount { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string Shape { get; set; } = string.Empty;
        public string ColorHex { get; set; } = string.Empty;
    }
}
