using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using POS_UI.Models;
using System.Collections.ObjectModel;

namespace POS_UI.Services
{
    public class DraftStorageService
    {
        private static readonly string DraftsFilePath = POS_UI.Services.PathService.GetDraftsFilePath();
        private static readonly string DraftsDirectory = Path.GetDirectoryName(DraftsFilePath);

        public DraftStorageService()
        {
            // Ensure the directory exists
            if (!string.IsNullOrEmpty(DraftsDirectory) && !Directory.Exists(DraftsDirectory))
            {
                Directory.CreateDirectory(DraftsDirectory);
            }
        }

        /// <summary>
        /// Saves all draft orders to the local file
        /// </summary>
        /// <param name="draftOrders">Collection of draft orders to save</param>
        public void SaveDrafts(ObservableCollection<DraftOrderModel> draftOrders)
        {
            try
            {
                var draftsList = new List<DraftOrderModel>(draftOrders);
                var json = JsonSerializer.Serialize(draftsList, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(DraftsFilePath, json);
                Console.WriteLine($"Saved {draftsList.Count} draft orders to file: {DraftsFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving drafts: {ex.Message}");
                MessageBox.Show($"Failed to save draft orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Loads all draft orders from the local file
        /// </summary>
        /// <returns>Collection of loaded draft orders</returns>
        public ObservableCollection<DraftOrderModel> LoadDrafts()
        {
            try
            {
                if (!File.Exists(DraftsFilePath))
                {
                    Console.WriteLine("No drafts file found, returning empty collection");
                    return new ObservableCollection<DraftOrderModel>();
                }

                var json = File.ReadAllText(DraftsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Console.WriteLine("Drafts file is empty, returning empty collection");
                    return new ObservableCollection<DraftOrderModel>();
                }

                var draftsList = JsonSerializer.Deserialize<List<DraftOrderModel>>(json);
                var draftOrders = new ObservableCollection<DraftOrderModel>(draftsList ?? new List<DraftOrderModel>());
                
                Console.WriteLine($"Loaded {draftOrders.Count} draft orders from file: {DraftsFilePath}");
                return draftOrders;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading drafts: {ex.Message}");
                MessageBox.Show($"Failed to load draft orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new ObservableCollection<DraftOrderModel>();
            }
        }

        /// <summary>
        /// Clears all draft orders by deleting the file
        /// </summary>
        public void ClearAllDrafts()
        {
            try
            {
                if (File.Exists(DraftsFilePath))
                {
                    File.Delete(DraftsFilePath);
                    Console.WriteLine($"Deleted drafts file: {DraftsFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing drafts: {ex.Message}");
                MessageBox.Show($"Failed to clear draft orders: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Checks if there are any saved drafts
        /// </summary>
        /// <returns>True if drafts file exists and is not empty</returns>
        /*public bool HasDrafts()
        {
            try
            {
                if (!File.Exists(DraftsFilePath))
                    return false;

                var json = File.ReadAllText(DraftsFilePath);
                return !string.IsNullOrWhiteSpace(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for drafts: {ex.Message}");
                return false;
            }
        }*/

        /// <summary>
        /// Gets the number of saved drafts
        /// </summary>
        /// <returns>Number of drafts in the file</returns>
        /*public int GetDraftCount()
        {
            try
            {
                var drafts = LoadDrafts();
                return drafts.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting draft count: {ex.Message}");
                return 0;
            }
        }*/
    }
} 