using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class PrinterSettingsModel : INotifyPropertyChanged
    {
        private string _deviceName;
        private string _connectedVia;
        private bool _isActive;
        private string _paperSize;
        private int _printWidth;
        private bool _autoCut;
        private string _fontSize;
        private bool _boldHeader;
        private bool _showLogo;
        private string _customSettings;
        private bool _mainReceipt;
        private bool _kitchenReceipt;
        private bool _mainReceiptOnOrder;
        private int _mainReceiptCount;
        private int _kitchenReceiptCount;

        public string DeviceName 
        { 
            get => _deviceName; 
            set { _deviceName = value; OnPropertyChanged(); } 
        }
        
        public string ConnectedVia 
        { 
            get => _connectedVia; 
            set { _connectedVia = value; OnPropertyChanged(); } 
        }
        
        public bool IsActive 
        { 
            get => _isActive; 
            set { _isActive = value; OnPropertyChanged(); } 
        }
        
        public string PaperSize 
        { 
            get => _paperSize; 
            set { _paperSize = value; OnPropertyChanged(); } 
        }
        
        public int PrintWidth 
        { 
            get => _printWidth; 
            set { _printWidth = value; OnPropertyChanged(); } 
        }
        
        public bool AutoCut 
        { 
            get => _autoCut; 
            set { _autoCut = value; OnPropertyChanged(); } 
        }
        
        public string FontSize 
        { 
            get => _fontSize; 
            set { _fontSize = value; OnPropertyChanged(); } 
        }
        
        public bool BoldHeader 
        { 
            get => _boldHeader; 
            set { _boldHeader = value; OnPropertyChanged(); } 
        }
        
        public bool ShowLogo 
        { 
            get => _showLogo; 
            set { _showLogo = value; OnPropertyChanged(); } 
        }
        
        public string CustomSettings 
        { 
            get => _customSettings; 
            set { _customSettings = value; OnPropertyChanged(); } 
        }
        
        public bool MainReceipt 
        { 
            get => _mainReceipt; 
            set { _mainReceipt = value; OnPropertyChanged(); } 
        }
        
        public bool KitchenReceipt 
        { 
            get => _kitchenReceipt; 
            set { _kitchenReceipt = value; OnPropertyChanged(); } 
        }
        
        public bool MainReceiptOnOrder 
        { 
            get => _mainReceiptOnOrder; 
            set { _mainReceiptOnOrder = value; OnPropertyChanged(); } 
        }
        
        public int MainReceiptCount 
        { 
            get => _mainReceiptCount; 
            set { _mainReceiptCount = value; OnPropertyChanged(); } 
        }
        
        public int KitchenReceiptCount 
        { 
            get => _kitchenReceiptCount; 
            set { _kitchenReceiptCount = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public PrinterSettingsModel()
        {
            // Set default values
            PaperSize = "80mm";
            PrintWidth = 32;
            AutoCut = true;
            FontSize = "Normal";
            BoldHeader = true;
            ShowLogo = false;
            IsActive = true;
            MainReceipt = true;
            KitchenReceipt = false;
            MainReceiptOnOrder = true;
            MainReceiptCount = 1;
            KitchenReceiptCount = 1;
        }

        public PrinterSettingsModel(string deviceName, string connectedVia)
        {
            DeviceName = deviceName;
            ConnectedVia = connectedVia;
            
            // Set default values
            PaperSize = "80mm";
            PrintWidth = 32;
            AutoCut = true;
            FontSize = "Normal";
            BoldHeader = true;
            ShowLogo = false;
            IsActive = true;
            MainReceipt = true;
            KitchenReceipt = false;
            MainReceiptOnOrder = true;
            MainReceiptCount = 1;
            KitchenReceiptCount = 1;
        }
    }
}
