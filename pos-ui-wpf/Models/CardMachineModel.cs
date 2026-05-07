using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public enum CardDeviceType
    {
        PaymentSave,
        Ingenico,
        Verifone,
        PAX,
        SumUp,
        Square,
        Custom
    }

    public class CardMachineModel : INotifyPropertyChanged
    {
        public const string DEFAULT_DEVICE_TYPE = "PaymentSave";

        private string _deviceName;
        private string _deviceType = DEFAULT_DEVICE_TYPE;
        private string _apiEndpoint;
        private string _ipAddress;
        private string _port;
        private string _authToken;
        private string _deviceId;
        private int _paringCode;
        private DateTime _paringCodeTime;
        private bool _isActive;

        public string DeviceName 
        { 
            get => _deviceName; 
            set { _deviceName = value; OnPropertyChanged(); } 
        }
        
        public string DeviceType 
        { 
            get => _deviceType; 
            set { _deviceType = value; OnPropertyChanged(); } 
        }
        
        public string APIEndpoint 
        { 
            get => _apiEndpoint; 
            set { _apiEndpoint = value; OnPropertyChanged(); } 
        }
        
        public string IPAddress 
        { 
            get => _ipAddress; 
            set { _ipAddress = value; OnPropertyChanged(); } 
        }
        
        public string Port 
        { 
            get => _port; 
            set { _port = value; OnPropertyChanged(); } 
        }
        
        public string AuthToken 
        { 
            get => _authToken; 
            set { _authToken = value; OnPropertyChanged(); } 
        }
        
        public string DeviceId 
        { 
            get => _deviceId; 
            set { _deviceId = value; OnPropertyChanged(); } 
        }
        
        public int ParingCode 
        { 
            get => _paringCode; 
            set { _paringCode = value; OnPropertyChanged(); } 
        }
        
        public DateTime ParingCodeTime 
        { 
            get => _paringCodeTime; 
            set { _paringCodeTime = value; OnPropertyChanged(); } 
        }
        
        public bool IsActive 
        { 
            get => _isActive; 
            set 
            { 
                _isActive = value; 
                OnPropertyChanged();
                OnPropertyChanged(nameof(Status));
                OnPropertyChanged(nameof(StatusColor));
            } 
        }
        
        public string Status => IsActive ? "Active" : "Inactive";
        public string StatusColor => IsActive ? "#00C853" : "#FF5252";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public CardMachineModel()
        {
            DeviceType = DEFAULT_DEVICE_TYPE;
            SetDefaultAPIEndpoint();
        }

        public void SetDefaultAPIEndpoint()
        {
            if (string.IsNullOrEmpty(DeviceType))
            {
                DeviceType = DEFAULT_DEVICE_TYPE;
            }

            switch (DeviceType.ToLower())
            {
                case "paymentsave":
                    APIEndpoint = "/POSitiveWebLink/1.0.0/rest";
                    break;
                case "ingenico":
                    APIEndpoint = "https://api.ingenico.com/payment";
                    break;
                case "verifone":
                    APIEndpoint = "https://api.verifone.com/terminal";
                    break;
                case "pax":
                    APIEndpoint = "https://api.pax.com/device";
                    break;
                case "sumup":
                    APIEndpoint = "https://api.sumup.com/v0.1";
                    break;
                case "square":
                    APIEndpoint = "https://connect.squareup.com/v2";
                    break;
                case "custom":
                    // Custom endpoint should be set manually
                    break;
                default:
                    APIEndpoint = "/POSitiveWebLink/1.0.0/rest"; // Default to PaymentSave
                    break;
            }
        }

        public static CardMachineModel CreateDefault()
        {
            return new CardMachineModel
            {
                DeviceName = "Default Payment Terminal",
                DeviceType = DEFAULT_DEVICE_TYPE,
                IsActive = true
            };
        }
    }
}