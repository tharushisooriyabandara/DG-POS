using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System;
using System.Collections.ObjectModel;
namespace POS_UI.Models
{
    public class PlatformModel : INotifyPropertyChanged
    {
        private bool _isActive ; // Set to true by default
        private string _status;
        private bool _isUpdating;
        private bool _displayIsActive; // For UI display only
        public bool AutoAccepting { get; set; }
  public int PlatformId { get; set; } // maps to API platform_id
        public int Id { get; set; }
        public string PlatformName { get; set; }
        public string Branch { get; set; }
        public string Name { get; set; }
        public string PlatformLogo { get; set; }

        public PlatformModel()
        {
            _isActive = true;
            _displayIsActive = true; // Initialize display state
        }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(StatusColor));
                    
                    // Remove automatic display state update - we'll control this manually
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusColor => IsActive ? "#00C853" : "#FF5252";

        public bool IsUpdating
        {
            get => _isUpdating;
            set
            {
                if (_isUpdating != value)
                {
                    _isUpdating = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool DisplayIsActive
        {
            get => _displayIsActive;
            set
            {
                if (_displayIsActive != value)
                {
                    _displayIsActive = value;
                    OnPropertyChanged();
                }
            }
        }



// Selected snooze option for this platform
private string _selectedSnoozeOption;
public string SelectedSnoozeOption
{
    get => _selectedSnoozeOption;
    set { _selectedSnoozeOption = value; OnPropertyChanged(); }
}


        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Simple RelayCommand implementation
        public class RelayCommand : ICommand
        {
            private readonly Action _execute;
            private readonly Func<bool> _canExecute;

            public RelayCommand(Action execute, Func<bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;

            public void Execute(object parameter) => _execute();
        }
    }
}
