using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class PrinterGroupSelectionService
    {
        private static PrinterGroupSelectionService _instance;
        public static PrinterGroupSelectionService Instance => _instance ??= new PrinterGroupSelectionService();

        private readonly string _filePath;
        private ObservableCollection<PrinterGroupSelectionModel> _selections;

        private PrinterGroupSelectionService()
        {
            // Create file path in centralized ALLPOSDETAILS folder
            PathService.EnsureInitialized();
            _filePath = PathService.GetFilePath("printer_group_selections.txt");
            _selections = new ObservableCollection<PrinterGroupSelectionModel>();
            LoadSelections();
        }

        public ObservableCollection<PrinterGroupSelectionModel> Selections
        {
            get => _selections;
            set
            {
                _selections = value;
                SaveSelections();
            }
        }

        public void AddOrUpdateSelection(int printerGroupId, string printerGroupName, string printerName, bool isSelected)
        {
            var selection = _selections.FirstOrDefault(s => s.PrinterGroupId == printerGroupId);
            
            if (selection == null)
            {
                selection = new PrinterGroupSelectionModel
                {
                    PrinterGroupId = printerGroupId,
                    PrinterGroupName = printerGroupName
                };
                _selections.Add(selection);
            }
            else
            {
                // Update the group name in case it changed
                selection.PrinterGroupName = printerGroupName;
            }

            if (isSelected)
            {
                if (!selection.SelectedPrinterNames.Contains(printerName))
                {
                    selection.SelectedPrinterNames.Add(printerName);
                }
            }
            else
            {
                selection.SelectedPrinterNames.Remove(printerName);
            }

            SaveSelections();
            System.Diagnostics.Debug.WriteLine($"[PrinterGroupSelectionService] Updated selection for group {printerGroupId}: {printerName} = {isSelected}");
        }

        public bool IsPrinterSelectedForGroup(int printerGroupId, string printerName)
        {
            var selection = _selections.FirstOrDefault(s => s.PrinterGroupId == printerGroupId);
            return selection?.SelectedPrinterNames.Contains(printerName) ?? false;
        }

        public List<string> GetSelectedPrintersForGroup(int printerGroupId)
        {
            var selection = _selections.FirstOrDefault(s => s.PrinterGroupId == printerGroupId);
            return selection?.SelectedPrinterNames ?? new List<string>();
        }

        private void LoadSelections()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string jsonContent = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var selectionsList = JsonConvert.DeserializeObject<List<PrinterGroupSelectionModel>>(jsonContent);
                        _selections.Clear();
                        foreach (var selection in selectionsList)
                        {
                            _selections.Add(selection);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading printer group selections: {ex.Message}");
            }
        }

        private void SaveSelections()
        {
            try
            {
                var selectionsList = _selections.ToList();
                string jsonContent = JsonConvert.SerializeObject(selectionsList, Formatting.Indented);
                File.WriteAllText(_filePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving printer group selections: {ex.Message}");
            }
        }

        public void Refresh()
        {
            LoadSelections();
        }
    }
}
