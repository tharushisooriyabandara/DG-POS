using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class PrinterSettingsService
    {
        private static PrinterSettingsService _instance;
        public static PrinterSettingsService Instance => _instance ??= new PrinterSettingsService();

        private readonly string _filePath;
        private ObservableCollection<PrinterSettingsModel> _printerSettings;

        private PrinterSettingsService()
        {
            // Create file path in centralized ALLPOSDETAILS folder
            PathService.EnsureInitialized();
            _filePath = PathService.GetFilePath("printers.txt");
            _printerSettings = new ObservableCollection<PrinterSettingsModel>();
            LoadPrinterSettings();
        }

        public ObservableCollection<PrinterSettingsModel> PrinterSettings
        {
            get => _printerSettings;
            set
            {
                _printerSettings = value;
                SavePrinterSettings();
            }
        }

        public void AddPrinterSettings(PrinterSettingsModel printerSettings)
        {
            if (printerSettings == null) return;

            _printerSettings.Add(printerSettings);
            SavePrinterSettings();
            System.Diagnostics.Debug.WriteLine($"[PrinterSettingsService] Added settings for printer: {printerSettings.DeviceName}");
        }

        public void UpdatePrinterSettings(PrinterSettingsModel printerSettings)
        {
            if (printerSettings == null) return;

            var existingSettings = _printerSettings.FirstOrDefault(p => p.DeviceName == printerSettings.DeviceName);
            if (existingSettings != null)
            {
                var index = _printerSettings.IndexOf(existingSettings);
                _printerSettings[index] = printerSettings;
                SavePrinterSettings();
                System.Diagnostics.Debug.WriteLine($"[PrinterSettingsService] Updated settings for printer: {printerSettings.DeviceName}");
            }
        }

        public void DeletePrinterSettings(PrinterSettingsModel printerSettings)
        {
            if (printerSettings == null) return;

            _printerSettings.Remove(printerSettings);
            SavePrinterSettings();
        }

        public PrinterSettingsModel GetPrinterSettings(string deviceName)
        {
            return _printerSettings.FirstOrDefault(p => p.DeviceName == deviceName);
        }

        public bool HasPrinterSettings(string deviceName)
        {
            return _printerSettings.Any(p => p.DeviceName == deviceName);
        }

        private void LoadPrinterSettings()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string jsonContent = File.ReadAllText(_filePath);
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        var printerSettingsList = JsonConvert.DeserializeObject<List<PrinterSettingsModel>>(jsonContent);
                        _printerSettings.Clear();
                        foreach (var settings in printerSettingsList)
                        {
                            _printerSettings.Add(settings);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading printer settings: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void SavePrinterSettings()
        {
            try
            {
                var printerSettingsList = _printerSettings.ToList();
                string jsonContent = JsonConvert.SerializeObject(printerSettingsList, Formatting.Indented);
                File.WriteAllText(_filePath, jsonContent);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving printer settings: {ex.Message}", "Error", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        public void Refresh()
        {
            LoadPrinterSettings();
        }
    }
}
