using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace POS_UI.Models
{
    public class CardMachineUserModel : INotifyPropertyChanged
    {
        private string _userId;
        private string _userName;
        private string _password;
        private bool _supervisor;
        private string _tid;
        private DateTime _createdAt;

        public string UserId 
        { 
            get => _userId; 
            set { _userId = value; OnPropertyChanged(); } 
        }
        
        public string UserName 
        { 
            get => _userName; 
            set { _userName = value; OnPropertyChanged(); } 
        }
        
        public string Password 
        { 
            get => _password; 
            set { _password = value; OnPropertyChanged(); } 
        }
        
        public bool Supervisor 
        { 
            get => _supervisor; 
            set { _supervisor = value; OnPropertyChanged(); } 
        }
        
        public string Tid 
        { 
            get => _tid; 
            set { _tid = value; OnPropertyChanged(); } 
        }
        
        public DateTime CreatedAt 
        { 
            get => _createdAt; 
            set { _createdAt = value; OnPropertyChanged(); } 
        }

        public string SupervisorText => Supervisor ? "Yes" : "No";
        public string SupervisorColor => Supervisor ? "#4CAF50" : "#9E9E9E";

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => 
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public CardMachineUserModel()
        {
            CreatedAt = DateTime.Now;
        }
    }
} 