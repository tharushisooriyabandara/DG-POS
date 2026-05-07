using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using POS_UI.Models;

namespace POS_UI.Helpers
{
    /// <summary>Result of parsing GET /config/floor_plan <c>config</c> payload.</summary>
    public sealed class FloorPlanConfigParseResult
    {
        public bool FloorPlanLayoutEnabled { get; init; }
        public List<FloorPlanModel> Plans { get; init; } = new List<FloorPlanModel>();

        /// <summary>Merged built-in + API floor plan custom item definitions.</summary>
        public List<FloorPlanCustomItemTypeModel> CustomItemTypes { get; init; } = new List<FloorPlanCustomItemTypeModel>();
    }

    /// <summary>
    /// Parses GET /config/floor_plan JSON into floor plan models (same rules as Settings floor plan list).
    /// </summary>
    public static class FloorPlanGetConfigParser
    {
        /// <summary>
        /// Returns parsed config, an empty plan list when the response is empty/valid-but-no-plans, or <c>null</c> when JSON cannot be deserialized.
        /// </summary>
        public static FloorPlanConfigParseResult? TryParse(string? json)
        {
            var builtInOnlyTypes = FloorPlanCustomItemCatalog.MergeWithApi(null);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new FloorPlanConfigParseResult { FloorPlanLayoutEnabled = false, Plans = new List<FloorPlanModel>(), CustomItemTypes = builtInOnlyTypes };
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("data", out var dataEl))
            {
                return new FloorPlanConfigParseResult { FloorPlanLayoutEnabled = false, Plans = new List<FloorPlanModel>(), CustomItemTypes = builtInOnlyTypes };
            }

            JsonElement planPayloadEl = dataEl;
            if (dataEl.TryGetProperty("config", out var configEl) && configEl.ValueKind == JsonValueKind.Object)
            {
                planPayloadEl = configEl;
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            FloorPlanConfigDataModel? payload;
            try
            {
                payload = JsonSerializer.Deserialize<FloorPlanConfigDataModel>(planPayloadEl.GetRawText(), options);
            }
            catch
            {
                return null;
            }

            if (payload == null)
            {
                return new FloorPlanConfigParseResult { FloorPlanLayoutEnabled = false, Plans = new List<FloorPlanModel>(), CustomItemTypes = builtInOnlyTypes };
            }

            var enabled = payload.FloorPlanLayoutEnabled;
            var mergedTypes = FloorPlanCustomItemCatalog.MergeWithApi(payload.FloorPlanCustomItemTypes);
            var resultPlans = new List<FloorPlanModel>();
            if (payload.FloorPlans == null || payload.FloorPlans.Count == 0)
            {
                return new FloorPlanConfigParseResult
                {
                    FloorPlanLayoutEnabled = enabled,
                    Plans = resultPlans,
                    CustomItemTypes = mergedTypes
                };
            }

            foreach (var item in payload.FloorPlans.OrderBy(f => f.FloorPlanId))
            {
                var fp = new FloorPlanModel
                {
                    Id = item.FloorPlanId,
                    Name = string.IsNullOrWhiteSpace(item.Name) ? $"Floor Plan {item.FloorPlanId}" : item.Name
                };

                foreach (var t in item.Tables ?? Enumerable.Empty<FloorPlanSaveTableModel>())
                {
                    var shape = FloorPlanShapeType.Rectangle;
                    if (!string.IsNullOrWhiteSpace(t.Shape))
                    {
                        Enum.TryParse(t.Shape, ignoreCase: true, out shape);
                    }

                    fp.Tables.Add(new FloorPlanTablePlacementModel
                    {
                        Kind = FloorPlanElementKind.Table,
                        TableId = t.TableId,
                        TableName = t.Name ?? string.Empty,
                        SeatCount = t.SeatCount,
                        X = t.X,
                        Y = t.Y,
                        Width = t.Width,
                        Height = t.Height,
                        Shape = shape,
                        ColorHex = string.IsNullOrWhiteSpace(t.ColorHex) ? "#4CAF50" : t.ColorHex
                    });
                }

                foreach (var c in item.CustomItems ?? Enumerable.Empty<FloorPlanSaveCustomItemModel>())
                {
                    fp.Tables.Add(ToPlacementFromSaveCustom(c));
                }

                resultPlans.Add(fp);
            }

            return new FloorPlanConfigParseResult
            {
                FloorPlanLayoutEnabled = enabled,
                Plans = resultPlans,
                CustomItemTypes = mergedTypes
            };
        }

        private static FloorPlanTablePlacementModel ToPlacementFromSaveCustom(FloorPlanSaveCustomItemModel c)
        {
            var shape = FloorPlanShapeType.Rectangle;
            if (!string.IsNullOrWhiteSpace(c.Shape))
            {
                Enum.TryParse(c.Shape, ignoreCase: true, out shape);
            }

            return new FloorPlanTablePlacementModel
            {
                Kind = FloorPlanElementKind.CustomItem,
                InstanceId = string.IsNullOrWhiteSpace(c.InstanceId) ? Guid.NewGuid().ToString("N") : c.InstanceId,
                ItemTypeKey = c.ItemTypeKey ?? string.Empty,
                IconKindName = string.IsNullOrWhiteSpace(c.IconKind) ? "MapMarker" : c.IconKind,
                TableId = 0,
                TableName = string.IsNullOrWhiteSpace(c.Label) ? (c.ItemTypeKey ?? "Item") : c.Label,
                SeatCount = 0,
                X = c.X,
                Y = c.Y,
                Width = c.Width <= 0 ? 80 : c.Width,
                Height = c.Height <= 0 ? 56 : c.Height,
                Shape = shape,
                ColorHex = string.IsNullOrWhiteSpace(c.ColorHex) ? "#78909C" : c.ColorHex
            };
        }
    }
}
