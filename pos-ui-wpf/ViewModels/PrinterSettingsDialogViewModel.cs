using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    public class PrinterSettingsDialogViewModel : INotifyPropertyChanged
    {
        private StatusOption _selectedStatus;
        private PrinterModel _printer;
        private PrinterSettingsModel _printerSettings;

        public PrinterModel Printer
        {
            get => _printer;
            set
            {
                _printer = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DeviceName));
                OnPropertyChanged(nameof(ConnectedVia));
            }
        }

        public string DeviceName => Printer?.DeviceName;
        public string ConnectedVia => Printer?.ConnectedVia;

        private ObservableCollection<StatusOption> _statusOptions;
        public ObservableCollection<StatusOption> StatusOptions
        {
            get => _statusOptions;
            set
            {
                _statusOptions = value;
                OnPropertyChanged();
            }
        }

        public StatusOption SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                _selectedStatus = value;
                OnPropertyChanged();
            }
        }

        // Receipt Configuration Properties
        public bool MainReceipt
        {
            get => _printerSettings?.MainReceipt ?? true;
            set
            {
                if (_printerSettings != null)
                {
                    _printerSettings.MainReceipt = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool KitchenReceipt
        {
            get => _printerSettings?.KitchenReceipt ?? false;
            set
            {
                if (_printerSettings != null)
                {
                    _printerSettings.KitchenReceipt = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool MainReceiptOnOrder
        {
            get => _printerSettings?.MainReceiptOnOrder ?? true;
            set
            {
                if (_printerSettings != null)
                {
                    _printerSettings.MainReceiptOnOrder = value;
                    OnPropertyChanged();
                }
            }
        }

        public int MainReceiptCount
        {
            get => _printerSettings?.MainReceiptCount ?? 1;
            set
            {
                if (_printerSettings != null)
                {
                    _printerSettings.MainReceiptCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public int KitchenReceiptCount
        {
            get => _printerSettings?.KitchenReceiptCount ?? 1;
            set
            {
                if (_printerSettings != null)
                {
                    _printerSettings.KitchenReceiptCount = value;
                    OnPropertyChanged();
                }
            }
        }

        public PrinterSettingsDialogViewModel(PrinterModel printer)
        {
            Printer = printer;
            
            // Load or create printer settings
            _printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
            if (_printerSettings == null)
            {
                _printerSettings = new PrinterSettingsModel(printer.DeviceName, printer.ConnectedVia);
                PrinterSettingsService.Instance.AddPrinterSettings(_printerSettings);
            }
            
            StatusOptions = new ObservableCollection<StatusOption>
            {
                new StatusOption { Name = "Active", Color = "#00C853" },
                new StatusOption { Name = "Inactive", Color = "#FF5252" }
            };

            // Set the current status as selected
            SelectedStatus = Printer.IsActive ? StatusOptions[0] : StatusOptions[1];
            
            // Trigger property change notifications
            OnPropertyChanged(nameof(StatusOptions));
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(MainReceipt));
            OnPropertyChanged(nameof(KitchenReceipt));
            OnPropertyChanged(nameof(MainReceiptOnOrder));
            OnPropertyChanged(nameof(MainReceiptCount));
            OnPropertyChanged(nameof(KitchenReceiptCount));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public void SaveSettings()
        {
            if (_printerSettings != null)
            {
                // Update printer active status
                Printer.IsActive = SelectedStatus.Name == "Active";
                _printerSettings.IsActive = Printer.IsActive;
                
                // Save to service
                PrinterSettingsService.Instance.UpdatePrinterSettings(_printerSettings);
            }
        }
    }
} 