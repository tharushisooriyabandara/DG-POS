using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using POS_UI.Models;

namespace POS_UI.Services
{
    /// <summary>
    /// Service for managing cashier menu configuration (tabs, categories, items)
    /// Handles saving/loading menu layout from API
    /// </summary>
    public class MenuConfigService
    {
        private static MenuConfigService _instance;
        private static readonly object _lock = new object();
        private MenuConfigModel _currentConfig;
        private readonly ApiService _apiService;

        public static MenuConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MenuConfigService();
                        }
                    }
                }
                return _instance;
            }
        }

        private MenuConfigService()
        {
            _apiService = new ApiService();
        }

        /// <summary>
        /// Gets the current menu configuration (from memory cache)
        /// </summary>
        public MenuConfigModel GetCurrentConfig()
        {
            return _currentConfig;
        }

        /// <summary>
        /// Loads menu configuration from API
        /// Returns default configuration if API fails or no config exists
        /// </summary>
        public async Task<MenuConfigModel> LoadMenuConfigAsync()
        {
            try
            {
                // Get shop info
                var localStorage = new LocalStorageService();
                var shopDetails = localStorage.GetShopDetails();
                if (shopDetails == null)
                {
                    throw new Exception("Shop details not found in local storage");
                }

                // Get brand ID from settings
                var settingsService = new SettingsService();
                var (_, _, brandIdStr) = settingsService.LoadSettings();
                if (!int.TryParse(brandIdStr, out int brandId))
                {
                    throw new Exception("Invalid brand ID in settings");
                }

                int shopId = shopDetails.Id;
                string terminalId = "1"; // Hardcoded for now as requested

                System.Diagnostics.Debug.WriteLine($"[MenuConfig] Loading from API: shopId={shopId}, brandId={brandId}, terminalId={terminalId}");
                
                // Call API to get menu config
                var json = await _apiService.GetMenuConfigAsync(shopId, brandId, terminalId);
                
                System.Diagnostics.Debug.WriteLine($"[MenuConfig] API Response: {json ?? "NULL"}");
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    // No config exists yet, create default
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] No config found, creating default");
                    var defaultConfig = CreateDefaultConfiguration(brandId, shopId, terminalId);
                    _currentConfig = defaultConfig;
                    return defaultConfig;
                }
                
                // Parse the response - handle multiple formats
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                JsonElement dataElement;
                JsonElement tabsElement;
                
                // Try to find the tabs array in various possible locations
                if (root.TryGetProperty("data", out dataElement))
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Found 'data' property");
                    
                    // Check if data has config.tabs (nested structure)
                    if (dataElement.TryGetProperty("config", out var configElement))
                    {
                        System.Diagnostics.Debug.WriteLine($"[MenuConfig] Found 'config' inside 'data'");
                        
                        if (configElement.TryGetProperty("tabs", out tabsElement))
                        {
                            System.Diagnostics.Debug.WriteLine($"[MenuConfig] Found 'tabs' inside 'data.config'");
                            dataElement = configElement; // Use config as the data element for brandId, outletId, etc.
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[MenuConfig] No 'tabs' in 'data.config'");
                            tabsElement = configElement;
                        }
                    }
                    // Check if data has tabs directly
                    else if (dataElement.TryGetProperty("tabs", out tabsElement))
                    {
                        System.Diagnostics.Debug.WriteLine($"[MenuConfig] Found 'tabs' inside 'data'");
                    }
                    else if (dataElement.ValueKind == JsonValueKind.Array)
                    {
                        // data IS the tabs array
                        tabsElement = dataElement;
                        System.Diagnostics.Debug.WriteLine($"[MenuConfig] 'data' is the tabs array");
                    }
                    else
                    {
                        // data is a single object, treat as root
                        dataElement = root.GetProperty("data");
                        tabsElement = dataElement;
                        System.Diagnostics.Debug.WriteLine($"[MenuConfig] 'data' is a single config object");
                    }
                }
                else if (root.TryGetProperty("tabs", out tabsElement))
                {
                    // tabs at root level
                    dataElement = root;
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Found 'tabs' at root level");
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // Root IS the tabs array
                    tabsElement = root;
                    dataElement = root;
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Root is the tabs array");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Cannot find tabs in response, creating default");
                    var defaultConfig = CreateDefaultConfiguration(brandId, shopId, terminalId);
                    _currentConfig = defaultConfig;
                    return defaultConfig;
                }

                // Create config
                MenuConfigModel config = new MenuConfigModel
                {
                    BrandId = dataElement.TryGetProperty("brandId", out var bid) ? bid.GetInt32() : brandId,
                    OutletId = dataElement.TryGetProperty("outletId", out var oid) ? oid.GetInt32() : shopId,
                    TerminalId = dataElement.TryGetProperty("terminalId", out var tid) ? tid.GetString() : terminalId,
                    Tabs = new List<MenuTabModel>()
                };

                // Parse tabs
                if (tabsElement.ValueKind == JsonValueKind.Array)
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Parsing {tabsElement.GetArrayLength()} tabs from array");
                    
                    foreach (var tabElement in tabsElement.EnumerateArray())
                    {
                        try
                        {
                            var tab = new MenuTabModel
                            {
                                Id = tabElement.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                                Name = tabElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unnamed",
                                Order = tabElement.TryGetProperty("order", out var orderProp) ? orderProp.GetInt32() : 1,
                                IsDefault = tabElement.TryGetProperty("isDefault", out var defProp) ? defProp.GetBoolean() : false,
                                ContentType = tabElement.TryGetProperty("contentType", out var typeProp) ? typeProp.GetString() : "categories"
                            };

                            // Parse category IDs
                            if (tabElement.TryGetProperty("categoryIds", out var categoryIdsElement) && categoryIdsElement.ValueKind == JsonValueKind.Array)
                            {
                                tab.CategoryIds = categoryIdsElement.EnumerateArray()
                                    .Select(e => e.GetInt32())
                                    .ToList();
                            }

                            // Parse item IDs
                            if (tabElement.TryGetProperty("itemIds", out var itemIdsElement) && itemIdsElement.ValueKind == JsonValueKind.Array)
                            {
                                tab.ItemIds = itemIdsElement.EnumerateArray()
                                    .Select(e => e.GetInt32())
                                    .ToList();
                            }

                            // Parse slots (used by mixed tabs to preserve category/item ordering)
                            if (tabElement.TryGetProperty("slots", out var slotsElement) && slotsElement.ValueKind == JsonValueKind.Array)
                            {
                                tab.Slots = new List<MenuSlotEntry>();
                                foreach (var slotEl in slotsElement.EnumerateArray())
                                {
                                    var slotType = slotEl.TryGetProperty("type", out var stProp) ? stProp.GetString() : null;
                                    var slotId = slotEl.TryGetProperty("id", out var siProp) ? siProp.GetInt32() : 0;
                                    if (!string.IsNullOrEmpty(slotType) && slotId > 0)
                                    {
                                        tab.Slots.Add(new MenuSlotEntry { Type = slotType, Id = slotId });
                                    }
                                }
                                System.Diagnostics.Debug.WriteLine($"[MenuConfig]   Parsed {tab.Slots.Count} slots for tab '{tab.Name}'");
                            }
                            else if (tab.ContentType == "mixed")
                            {
                                // Fallback: reconstruct slots from CategoryIds + ItemIds
                                tab.Slots = new List<MenuSlotEntry>();
                                foreach (var catId in tab.CategoryIds)
                                    tab.Slots.Add(new MenuSlotEntry { Type = "category", Id = catId });
                                foreach (var itemId in tab.ItemIds)
                                    tab.Slots.Add(new MenuSlotEntry { Type = "item", Id = itemId });
                                System.Diagnostics.Debug.WriteLine($"[MenuConfig]   Reconstructed {tab.Slots.Count} slots from IDs for mixed tab '{tab.Name}'");
                            }

                            config.Tabs.Add(tab);
                            System.Diagnostics.Debug.WriteLine($"[MenuConfig] ✓ Parsed tab: '{tab.Name}' (ID={tab.Id}, Order={tab.Order}, Type={tab.ContentType}, Default={tab.IsDefault})");
                        }
                        catch (Exception tabEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MenuConfig] ✗ Error parsing tab: {tabEx.Message}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Tabs element is not an array, type: {tabsElement.ValueKind}");
                }
                
                // Ensure at least one default tab exists
                if (config.Tabs.Count == 0 || !config.Tabs.Any(t => t.IsDefault))
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] No tabs found or no default tab, creating default");
                    config.GetOrCreateDefaultTab();
                }
                
                _currentConfig = config;
                System.Diagnostics.Debug.WriteLine($"[MenuConfig] ✓ Loaded successfully: {config.Tabs.Count} tabs");
                
                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuConfig] Error loading from API: {ex.Message}");
                
                // Return default configuration on error
                try
                {
                    var localStorage = new LocalStorageService();
                    var shopDetails = localStorage.GetShopDetails();
                    var settingsService = new SettingsService();
                    var (_, _, brandIdStr) = settingsService.LoadSettings();
                    int.TryParse(brandIdStr, out int brandId);
                    
                    var defaultConfig = CreateDefaultConfiguration(brandId, shopDetails?.Id ?? 0, "1");
                    _currentConfig = defaultConfig;
                    return defaultConfig;
                }
                catch
                {
                    // Last resort: return minimal config
                    var minimalConfig = CreateDefaultConfiguration(0, 0, "1");
                    _currentConfig = minimalConfig;
                    return minimalConfig;
                }
            }
        }

        /// <summary>
        /// Saves menu configuration to API
        /// </summary>
        public async Task<bool> SaveMenuConfigAsync(MenuConfigModel config)
        {
            try
            {
                // Validate configuration
                if (!config.IsValid(out string errorMessage))
                {
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Validation failed: {errorMessage}");
                    throw new InvalidOperationException(errorMessage);
                }

                // Get shop info
                var localStorage = new LocalStorageService();
                var shopDetails = localStorage.GetShopDetails();
                if (shopDetails == null)
                {
                    throw new Exception("Shop details not found in local storage");
                }

                // Get brand ID from settings
                var settingsService = new SettingsService();
                var (_, _, brandIdStr) = settingsService.LoadSettings();
                if (!int.TryParse(brandIdStr, out int brandId))
                {
                    throw new Exception("Invalid brand ID in settings");
                }

                int shopId = shopDetails.Id;
                string terminalId = config.TerminalId ?? "1";

                System.Diagnostics.Debug.WriteLine($"[MenuConfig] Saving to API: {config.Tabs.Count} tabs");
                
                // Serialize to JSON (camelCase for API)
                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                var json = JsonSerializer.Serialize(config, options);
                
                System.Diagnostics.Debug.WriteLine($"[MenuConfig] JSON: {json}");
                
                // Call API to save menu config
                var success = await _apiService.SaveMenuConfigAsync(shopId, brandId, json, terminalId);
                
                if (success)
                {
                    _currentConfig = config;
                    System.Diagnostics.Debug.WriteLine($"[MenuConfig] Saved successfully");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MenuConfig] Error saving to API: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Creates a default menu configuration with one "All Items" tab
        /// </summary>
        public MenuConfigModel CreateDefaultConfiguration(int brandId, int outletId, string terminalId)
        {
            var config = new MenuConfigModel
            {
                BrandId = brandId,
                OutletId = outletId,
                TerminalId = terminalId ?? "POS-DEFAULT",
                Tabs = new List<MenuTabModel>
                {
                    new MenuTabModel
                    {
                        Id = 1,
                        Name = "All Items",
                        Order = 1,
                        IsDefault = true,
                        ContentType = "categories",
                        CategoryIds = new List<int>(), // Empty = all categories
                        ItemIds = new List<int>()
                    }
                }
            };

            return config;
        }

        /// <summary>
        /// Creates a new tab with auto-generated ID
        /// </summary>
        public MenuTabModel CreateNewTab(string name, string contentType)
        {
            if (_currentConfig == null)
            {
                throw new InvalidOperationException("No menu configuration loaded");
            }

            // Find next available ID
            var maxId = _currentConfig.Tabs.Any() ? _currentConfig.Tabs.Max(t => t.Id) : 0;
            
            // Find next available order
            var maxOrder = _currentConfig.Tabs.Any() ? _currentConfig.Tabs.Max(t => t.Order) : 0;

            var newTab = new MenuTabModel
            {
                Id = maxId + 1,
                Name = name,
                Order = maxOrder + 1,
                IsDefault = false,
                ContentType = contentType,
                CategoryIds = new List<int>(),
                ItemIds = new List<int>()
            };

            return newTab;
        }

        /// <summary>
        /// Adds a tab to the current configuration
        /// </summary>
        public bool AddTab(MenuTabModel tab)
        {
            if (_currentConfig == null)
            {
                System.Diagnostics.Debug.WriteLine("[MenuConfig] Cannot add tab - no configuration loaded");
                return false;
            }

            if (_currentConfig.Tabs.Count >= 5)
            {
                System.Diagnostics.Debug.WriteLine("[MenuConfig] Cannot add tab - maximum of 5 tabs reached");
                return false;
            }

            _currentConfig.Tabs.Add(tab);
            System.Diagnostics.Debug.WriteLine($"[MenuConfig] Added tab: {tab.Name}");
            return true;
        }

        /// <summary>
        /// Removes a tab from the current configuration
        /// </summary>
        public bool RemoveTab(int tabId)
        {
            if (_currentConfig == null)
            {
                return false;
            }

            var tab = _currentConfig.Tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null)
            {
                return false;
            }

            // Cannot remove default tab
            if (tab.IsDefault)
            {
                System.Diagnostics.Debug.WriteLine("[MenuConfig] Cannot remove default tab");
                return false;
            }

            _currentConfig.Tabs.Remove(tab);
            System.Diagnostics.Debug.WriteLine($"[MenuConfig] Removed tab: {tab.Name}");
            
            // Reorder remaining tabs
            ReorderTabs();
            
            return true;
        }

        /// <summary>
        /// Reorders tabs based on their Order property
        /// </summary>
        private void ReorderTabs()
        {
            if (_currentConfig == null) return;

            var orderedTabs = _currentConfig.Tabs.OrderBy(t => t.Order).ToList();
            for (int i = 0; i < orderedTabs.Count; i++)
            {
                orderedTabs[i].Order = i + 1;
            }
        }

        /// <summary>
        /// Moves a tab to a new position
        /// </summary>
        public bool MoveTab(int tabId, int newOrder)
        {
            if (_currentConfig == null) return false;

            var tab = _currentConfig.Tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null) return false;

            // Clamp to valid range
            newOrder = Math.Max(1, Math.Min(newOrder, _currentConfig.Tabs.Count));

            // Update orders
            foreach (var t in _currentConfig.Tabs.Where(t => t.Order >= newOrder && t.Id != tabId))
            {
                t.Order++;
            }

            tab.Order = newOrder;
            ReorderTabs();

            return true;
        }

        /// <summary>
        /// Clears the current configuration cache
        /// </summary>
        public void ClearCache()
        {
            _currentConfig = null;
        }
    }
}
