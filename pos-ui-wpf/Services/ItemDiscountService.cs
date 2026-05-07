using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace POS_UI.Services
{
    public class ItemDiscountService
    {
        private const string FileName = "item_discounts.txt";

        public List<decimal> LoadPresets()
        {
            try
            {
                var path = PathService.GetFilePath(FileName);
                if (!File.Exists(path))
                    return new List<decimal> { 10, 20 };

                var lines = File.ReadAllLines(path);
                var presets = new List<decimal>();
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && decimal.TryParse(trimmed, out decimal val) && val > 0 && val <= 100)
                        presets.Add(val);
                }
                return presets.Count > 0 ? presets : new List<decimal> { 10, 20 };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDiscountService] LoadPresets error: {ex.Message}");
                return new List<decimal> { 10, 20 };
            }
        }

        public void SavePresets(List<decimal> presets)
        {
            try
            {
                var path = PathService.GetFilePath(FileName);
                var lines = presets.Where(p => p > 0 && p <= 100).Select(p => p.ToString("G29"));
                File.WriteAllLines(path, lines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ItemDiscountService] SavePresets error: {ex.Message}");
            }
        }
    }
}
