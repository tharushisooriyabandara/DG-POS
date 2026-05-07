using System.Collections.ObjectModel;
using System.Management;
using System.Windows;
using POS_UI.Models;
using System.Linq;

namespace POS_UI.Services
{
    public class PrintersService
    {
        // Singleton instance
        private static PrintersService _instance;
        public static PrintersService Instance => _instance ??= new PrintersService();

        // The shared printers list
        public ObservableCollection<PrinterModel> Printers { get; } = new ObservableCollection<PrinterModel>();

        // The method to load printers (move your logic here)
        public void GetConnectedPrinters()
        {
            try
            {
                // Remove any existing POS printers from list
                var nonPosPrinters = Printers
                    .Where(p => !p.DeviceName.Contains("POS", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                Printers.Clear();

                foreach (var printer in nonPosPrinters)
                {
                    Printers.Add(printer);
                }

                bool anyConnected = false;
                // Scan installed printers
                var printerQuery = new ManagementObjectSearcher("SELECT * FROM Win32_Printer");

                foreach (ManagementObject printer in printerQuery.Get())
                {
                    string name = printer["Name"]?.ToString() ?? "";
                    string portName = printer["PortName"]?.ToString() ?? "";

                    // Only include POS printers
                    if (!name.Contains("POS", StringComparison.OrdinalIgnoreCase))
                        continue;

                    bool isOffline = (bool)(printer["WorkOffline"] ?? false);
                    int printerStatus = Convert.ToInt32(printer["PrinterStatus"] ?? 0); // 3 = ready

                    // Only include connected and ready printers
                    if (!isOffline && printerStatus == 3)
                    {
                        string connectionType = GetConnectionType(portName);

                        // Check if we have saved settings for this printer
                        var savedSettings = PrinterSettingsService.Instance.GetPrinterSettings(name);
                        
                        if (savedSettings != null)
                        {
                            // Use saved settings
                            Printers.Add(new PrinterModel
                            {
                                DeviceName = name,
                                ConnectedVia = connectionType,
                                IsActive = savedSettings.IsActive
                            });
                            System.Diagnostics.Debug.WriteLine($"[PrintersService] Loaded saved settings for printer: {name}, Active: {savedSettings.IsActive}");
                        }
                        else
                        {
                            // Create new printer with default settings
                            Printers.Add(new PrinterModel
                            {
                                DeviceName = name,
                                ConnectedVia = connectionType,
                                IsActive = true
                            });
                            
                            // Create and save default settings for this printer
                            var defaultSettings = new PrinterSettingsModel(name, connectionType);
                            PrinterSettingsService.Instance.AddPrinterSettings(defaultSettings);
                            System.Diagnostics.Debug.WriteLine($"[PrintersService] Created new settings for printer: {name}");
                        }
                        
                        anyConnected = true;
                    }
                }
                if (!anyConnected)
                {
                    //MessageBox.Show("No available printers found.", "Printer Connection", MessageBoxButton.OK, MessageBoxImage.Information);
                    var vm = POS_UI.ViewModels.StatusDialogViewModel.CreateWarning("Not Found", "No available printers found.");
                    var dlg = new POS_UI.View.StatusDialog { DataContext = vm };
                    MaterialDesignThemes.Wpf.DialogHost.Show(dlg, "RootDialog");
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error getting connected printers: " + ex.Message, "Printer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private string GetConnectionType(string portName)
        {
            if (portName.StartsWith("USB", StringComparison.OrdinalIgnoreCase))
                return "USB";
            else if (portName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase))
                return "Parallel Port";
            else if (portName.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
                return "Serial Port";
            else if (portName.StartsWith("TCP") || portName.Contains("IP"))
                return "Network";
            else
                return "Unknown";
        }
    }    
}
 