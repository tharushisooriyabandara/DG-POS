using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using POS_UI.Models;
using POS_UI.Services;
using System.Linq;

namespace POS_UI.ViewModels
{
    public class ConnectedPrinterGroupsDialogViewModel : INotifyPropertyChanged
    {
        private PrinterGroupModel _printerGroup;
        private ObservableCollection<PrinterWithSelectionModel> _connectedPrinters;
        private ICommand _testPrintCommand;

        public PrinterGroupModel PrinterGroup
        {
            get => _printerGroup;
            set
            {
                _printerGroup = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(GroupName));
                // LoadPrinterSelections() is called from LoadPrinters() after ConnectedPrinters is initialized
                if (_connectedPrinters != null)
                {
                    LoadPrinterSelections();
                }
            }
        }

        public string GroupName => PrinterGroup?.Name ?? "Unknown Group";

        public ObservableCollection<PrinterWithSelectionModel> ConnectedPrinters
        {
            get => _connectedPrinters;
            set
            {
                _connectedPrinters = value;
                OnPropertyChanged();
            }
        }

        public ICommand TestPrintCommand
        {
            get => _testPrintCommand;
            set
            {
                _testPrintCommand = value;
                OnPropertyChanged();
            }
        }

        public ConnectedPrinterGroupsDialogViewModel(PrinterGroupModel printerGroup, ICommand testPrintCommand = null)
        {
            PrinterGroup = printerGroup;
            TestPrintCommand = testPrintCommand;
            
            // Load all connected printers with selection state
            LoadPrinters();
        }

        private void LoadPrinters()
        {
            ConnectedPrinters = new ObservableCollection<PrinterWithSelectionModel>();

            var printerSnapshot = PrintersService.Instance.Printers.ToList();
            foreach (var printer in printerSnapshot)
            {
                var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                if (printerSettings != null && printerSettings.KitchenReceipt)
                {
                    var printerWithSelection = new PrinterWithSelectionModel
                    {
                        Printer = printer
                    };
                    ConnectedPrinters.Add(printerWithSelection);
                }
            }
            
            LoadPrinterSelections();
        }

        private void LoadPrinterSelections()
        {
            if (PrinterGroup == null || ConnectedPrinters == null) return;

            foreach (var printerWithSelection in ConnectedPrinters)
            {
                bool isSelected = PrinterGroupSelectionService.Instance.IsPrinterSelectedForGroup(
                    PrinterGroup.Id, 
                    printerWithSelection.DeviceName
                );
                printerWithSelection.IsSelected = isSelected;
            }
        }

        public void SavePrinterSelection(PrinterWithSelectionModel printerWithSelection)
        {
            if (PrinterGroup == null || printerWithSelection == null) return;
            
            PrinterGroupSelectionService.Instance.AddOrUpdateSelection(
                PrinterGroup.Id,
                PrinterGroup.Name,
                printerWithSelection.DeviceName,
                printerWithSelection.IsSelected
            );
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) 
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
