using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class LocalOrderStorageService
    {
        private static LocalOrderStorageService _instance;
        private static readonly object _lock = new object();
        private string _ordersFolderPath;

        public static LocalOrderStorageService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LocalOrderStorageService();
                        }
                    }
                }
                return _instance;
            }
        }

        private LocalOrderStorageService()
        {
            InitializeOrdersFolder();
        }

        /// <summary>
        /// Initializes the POS-Orders folder under the centralized base folder
        /// </summary>
        private void InitializeOrdersFolder()
        {
            try
            {
                // Use centralized ALLPOSDETAILS folder
                POS_UI.Services.PathService.EnsureInitialized();
                _ordersFolderPath = POS_UI.Services.PathService.GetOrdersFolderPath();

                // Create folder if it doesn't exist
                if (!Directory.Exists(_ordersFolderPath))
                {
                    Directory.CreateDirectory(_ordersFolderPath);
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Created POS-Orders folder at: {_ordersFolderPath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] POS-Orders folder already exists at: {_ordersFolderPath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error initializing orders folder: {ex.Message}");
                MessageBox.Show($"Failed to create POS-Orders folder.\n{ex.Message}", "Storage Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Gets the path to the POS-Orders folder
        /// </summary>
        public string OrdersFolderPath => _ordersFolderPath;

        /// <summary>
        /// Ensures the POS-Orders folder exists (called during login)
        /// </summary>
        public async Task<bool> EnsureOrdersFolderExistsAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(_ordersFolderPath))
                    {
                        InitializeOrdersFolder();
                    }

                    if (!Directory.Exists(_ordersFolderPath))
                    {
                        Directory.CreateDirectory(_ordersFolderPath);
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Created POS-Orders folder at: {_ordersFolderPath}");
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error ensuring orders folder exists: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Creates a subfolder for a specific date
        /// </summary>
        public async Task<string> CreateDateFolderAsync(DateTime date)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string dateFolderName = date.ToString("yyyy-MM-dd");
                    string dateFolderPath = Path.Combine(_ordersFolderPath, dateFolderName);

                    if (!Directory.Exists(dateFolderPath))
                    {
                        Directory.CreateDirectory(dateFolderPath);
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Created date folder: {dateFolderPath}");
                    }

                    return dateFolderPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error creating date folder: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Gets the path for today's orders folder
        /// </summary>
        public async Task<string> GetTodayOrdersFolderAsync()
        {
            return await CreateDateFolderAsync(DateTime.Today);
        }

        #region Dine-In Order File Operations

        /// <summary>
        /// Saves a dine-in order to a JSON file in the POS-Orders folder
        /// </summary>
        public async Task<bool> SaveDineInOrderAsync(DineInOrderModel order)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (order == null || string.IsNullOrEmpty(order.DisplayOrderId))
                    {
                        System.Diagnostics.Debug.WriteLine("[LocalOrderStorage] Cannot save order: Order is null or DisplayOrderId is empty");
                        return false;
                    }

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        System.Diagnostics.Debug.WriteLine("[LocalOrderStorage] Cannot save order: Failed to get today's folder");
                        return false;
                    }

                    // Create filename using DisplayOrderId
                    string fileName = $"{order.DisplayOrderId}.json";
                    string filePath = Path.Combine(todayFolder, fileName);

                    // Update last modified timestamp
                    order.LastModified = DateTime.Now;

                    // Serialize order to JSON
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    string jsonContent = JsonSerializer.Serialize(order, options);

                    // Write to file
                    File.WriteAllText(filePath, jsonContent);

                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Saved dine-in order: {order.DisplayOrderId} to {filePath}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error saving dine-in order: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Loads a dine-in order from JSON file
        /// </summary>
        public async Task<DineInOrderModel> LoadDineInOrderAsync(string displayOrderId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(displayOrderId))
                    {
                        return null;
                    }

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        return null;
                    }

                    // Create filename using DisplayOrderId
                    string fileName = $"{displayOrderId}.json";
                    string filePath = Path.Combine(todayFolder, fileName);

                    if (!File.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Order file not found: {filePath}");
                        return null;
                    }

                    // Read and deserialize JSON
                    string jsonContent = File.ReadAllText(filePath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    var order = JsonSerializer.Deserialize<DineInOrderModel>(jsonContent, options);
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Loaded dine-in order: {displayOrderId}");
                    return order;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error loading dine-in order: {ex.Message}");
                    return null;
                }
            });
        }

        /// <summary>
        /// Updates an existing dine-in order (for modifications)
        /// </summary>
        public async Task<bool> UpdateDineInOrderAsync(DineInOrderModel order)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (order == null || string.IsNullOrEmpty(order.DisplayOrderId))
                    {
                        return false;
                    }

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        return false;
                    }

                    // Create filename using DisplayOrderId
                    string fileName = $"{order.DisplayOrderId}.json";
                    string filePath = Path.Combine(todayFolder, fileName);

                    if (!File.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Cannot update order: File not found: {filePath}");
                        return false;
                    }

                    // Update last modified timestamp
                    order.LastModified = DateTime.Now;

                    // Serialize order to JSON
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    };

                    string jsonContent = JsonSerializer.Serialize(order, options);

                    // Write to file
                    File.WriteAllText(filePath, jsonContent);

                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Updated dine-in order: {order.DisplayOrderId}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error updating dine-in order: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Deletes a dine-in order file (when order is completed)
        /// </summary>
        public async Task<bool> DeleteDineInOrderAsync(string displayOrderId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(displayOrderId))
                    {
                        return false;
                    }

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        return false;
                    }

                    // Create filename using DisplayOrderId
                    string fileName = $"{displayOrderId}.json";
                    string filePath = Path.Combine(todayFolder, fileName);

                    if (!File.Exists(filePath))
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Cannot delete order: File not found: {filePath}");
                        return false;
                    }

                    // Delete the file
                    File.Delete(filePath);

                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Deleted dine-in order: {displayOrderId}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error deleting dine-in order: {ex.Message}");
                    return false;
                }
            });
        }

        /// <summary>
        /// Gets all active dine-in orders for today
        /// </summary>
        public async Task<List<DineInOrderModel>> GetAllActiveDineInOrdersAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var orders = new List<DineInOrderModel>();

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        return orders;
                    }

                    // Get all JSON files in today's folder
                    string[] jsonFiles = Directory.GetFiles(todayFolder, "*.json");

                    foreach (string filePath in jsonFiles)
                    {
                        try
                        {
                            string jsonContent = File.ReadAllText(filePath);
                            var options = new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            };

                            var order = JsonSerializer.Deserialize<DineInOrderModel>(jsonContent, options);
                            if (order != null && order.OrderStatus == "ACTIVE")
                            {
                                orders.Add(order);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error loading order from {filePath}: {ex.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Loaded {orders.Count} active dine-in orders");
                    return orders;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error loading all dine-in orders: {ex.Message}");
                    return new List<DineInOrderModel>();
                }
            });
        }

        /// <summary>
        /// Checks if a dine-in order file exists
        /// </summary>
        public async Task<bool> DineInOrderExistsAsync(string displayOrderId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(displayOrderId))
                    {
                        return false;
                    }

                    // Get today's folder
                    string todayFolder = GetTodayOrdersFolderAsync().Result;
                    if (string.IsNullOrEmpty(todayFolder))
                    {
                        return false;
                    }

                    // Create filename using DisplayOrderId
                    string fileName = $"{displayOrderId}.json";
                    string filePath = Path.Combine(todayFolder, fileName);

                    return File.Exists(filePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocalOrderStorage] Error checking if order exists: {ex.Message}");
                    return false;
                }
            });
        }

        #endregion
    }
}
