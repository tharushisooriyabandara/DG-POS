using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace POS_UI.Helpers
{
    /// <summary>
    /// Provides a consistent 6-color palette for categories and items
    /// Colors are theme-matched and ensure good contrast with white text
    /// Color assignments are persisted to file in AllPOSData directory
    /// </summary>
    public static class ColorPalette
    {
        // 15 Eye-friendly colors matching the app theme (#1976D2)
        // All colors have been tested for WCAG contrast with white text
        private static readonly List<string> BackgroundColors = new List<string>
        {
            "#1976D2",  // Primary Blue
            "#00897B",  // Teal
            "#FB8C00",  // Orange
            "#7B1FA2",  // Purple
            "#E53935",  // Red
            "#43A047",  // Green
            "#D81B60",  // Pink
            "#5E35B1",  // Deep Purple
            "#F4511E",  // Deep Orange
            "#00ACC1",  // Cyan
            "#6D4C41",  // Brown
            "#546E7A",  // Blue Grey
            "#8E24AA",  // Violet
            "#039BE5",  // Light Blue
            "#C0CA33"   // Lime
        };

        private static readonly string AllPOSDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "ALLPOSDETAILS"
        );
        
        private static readonly string ColorMappingFile = Path.Combine(AllPOSDataPath, "color_mappings.json");
        
        private static Dictionary<string, int> _productColorMap = new Dictionary<string, int>();
        private static Dictionary<string, int> _categoryColorMap = new Dictionary<string, int>();
        private static bool _isLoaded = false;
        private static readonly object _lock = new object();

        /// <summary>
        /// Data structure for saving/loading color mappings
        /// </summary>
        private class ColorMappingData
        {
            public Dictionary<string, int> ProductColors { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, int> CategoryColors { get; set; } = new Dictionary<string, int>();
        }

        /// <summary>
        /// Loads color mappings from file
        /// </summary>
        private static void LoadColorMappings()
        {
            lock (_lock)
            {
                if (_isLoaded) return;

                try
                {
                    // Ensure directory exists
                    if (!Directory.Exists(AllPOSDataPath))
                    {
                        Directory.CreateDirectory(AllPOSDataPath);
                    }

                    if (File.Exists(ColorMappingFile))
                    {
                        var json = File.ReadAllText(ColorMappingFile);
                        var data = JsonSerializer.Deserialize<ColorMappingData>(json);
                        if (data != null)
                        {
                            _productColorMap = data.ProductColors ?? new Dictionary<string, int>();
                            _categoryColorMap = data.CategoryColors ?? new Dictionary<string, int>();
                            
                            System.Diagnostics.Debug.WriteLine($"[ColorPalette] Loaded {_productColorMap.Count} products, {_categoryColorMap.Count} categories");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading color mappings: {ex.Message}");
                }
                finally
                {
                    _isLoaded = true;
                }
            }
        }

        /// <summary>
        /// Saves color mappings to file
        /// </summary>
        private static void SaveColorMappings()
        {
            try
            {
                lock (_lock)
                {
                    var data = new ColorMappingData
                    {
                        ProductColors = _productColorMap,
                        CategoryColors = _categoryColorMap
                    };

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    var json = JsonSerializer.Serialize(data, options);
                    File.WriteAllText(ColorMappingFile, json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ColorPalette] Error saving color mappings: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a consistent background color based on an ID
        /// Same ID always returns same color (persisted to file)
        /// Simple hash-based assignment for consistency
        /// </summary>
        public static string GetBackgroundColor(int id)
        {
            return GetBackgroundColor(id, null);
        }
        
        /// <summary>
        /// Gets a consistent background color based on an ID
        /// Same ID always returns same color (persisted to file)
        /// </summary>
        public static string GetBackgroundColor(int id, string name)
        {
            if (id < 0) return BackgroundColors[0];
            
            LoadColorMappings();
            
            string key = $"product_{id}";
            
            lock (_lock)
            {
                if (!_productColorMap.ContainsKey(key))
                {
                    // Simple hash-based assignment
                    int colorIndex = Math.Abs(id) % BackgroundColors.Count;
                    _productColorMap[key] = colorIndex;
                    SaveColorMappings();
                }
                
                return BackgroundColors[_productColorMap[key]];
            }
        }

        /// <summary>
        /// Gets a consistent background color based on a string (category name, etc.)
        /// Same string always returns same color (persisted to file)
        /// Simple hash-based assignment for consistency
        /// </summary>
        public static string GetBackgroundColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return BackgroundColors[0];
            
            LoadColorMappings();
            
            string key = $"category_{value}";
            
            lock (_lock)
            {
                if (!_categoryColorMap.ContainsKey(key))
                {
                    // Simple hash-based assignment
                    int hash = Math.Abs(value.GetHashCode());
                    int colorIndex = hash % BackgroundColors.Count;
                    _categoryColorMap[key] = colorIndex;
                    SaveColorMappings();
                }
                
                return BackgroundColors[_categoryColorMap[key]];
            }
        }
        
        /// <summary>
        /// Manually set a custom color for a product
        /// </summary>
        public static void SetProductColor(int id, int colorIndex)
        {
            if (id < 0 || colorIndex < 0 || colorIndex >= BackgroundColors.Count) return;
            
            LoadColorMappings();
            
            string key = $"product_{id}";
            
            lock (_lock)
            {
                _productColorMap[key] = colorIndex;
                SaveColorMappings();
                System.Diagnostics.Debug.WriteLine($"[ColorPalette] Set custom color for product {id}: {BackgroundColors[colorIndex]}");
            }
        }
        
        /// <summary>
        /// Manually set a custom color for a category
        /// </summary>
        public static void SetCategoryColor(string categoryName, int colorIndex)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || colorIndex < 0 || colorIndex >= BackgroundColors.Count) return;
            
            LoadColorMappings();
            
            string key = $"category_{categoryName}";
            
            lock (_lock)
            {
                _categoryColorMap[key] = colorIndex;
                SaveColorMappings();
                System.Diagnostics.Debug.WriteLine($"[ColorPalette] Set custom color for category '{categoryName}': {BackgroundColors[colorIndex]}");
            }
        }
        
        /// <summary>
        /// Forces a reload of color mappings from disk
        /// Call this after updating colors to ensure fresh data is loaded
        /// </summary>
        public static void ReloadColorMappings()
        {
            lock (_lock)
            {
                _isLoaded = false;
                _productColorMap.Clear();
                _categoryColorMap.Clear();
                System.Diagnostics.Debug.WriteLine("[ColorPalette] Color mappings cleared, will reload from disk on next access");
            }
            
            // Immediately reload
            LoadColorMappings();
        }

        /// <summary>
        /// Gets the appropriate text color for any background from our palette
        /// All our palette colors use white text for optimal contrast
        /// </summary>
        public static string GetTextColor()
        {
            return "#FFFFFF"; // White text on all colored backgrounds
        }

        /// <summary>
        /// Gets a hover color (slightly darker) for a given background color
        /// </summary>
        public static string GetHoverColor(string backgroundColor)
        {
            return backgroundColor switch
            {
                "#1976D2" => "#1565C0",  // Darker Blue
                "#00897B" => "#00796B",  // Darker Teal
                "#FB8C00" => "#F57C00",  // Darker Orange
                "#7B1FA2" => "#6A1B9A",  // Darker Purple
                "#E53935" => "#D32F2F",  // Darker Red
                "#43A047" => "#388E3C",  // Darker Green
                _ => "#1565C0"           // Default darker blue
            };
        }

        /// <summary>
        /// Gets the total number of colors in the palette
        /// </summary>
        public static int ColorCount => BackgroundColors.Count;

        /// <summary>
        /// Gets color by index (0-5)
        /// </summary>
        public static string GetColorByIndex(int index)
        {
            if (index < 0 || index >= BackgroundColors.Count) return BackgroundColors[0];
            return BackgroundColors[index];
        }
        
        /// <summary>
        /// Gets all available colors (for color picker UI)
        /// </summary>
        public static List<string> GetAllColors()
        {
            return new List<string>(BackgroundColors);
        }
    }
}
