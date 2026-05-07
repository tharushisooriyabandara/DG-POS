using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class TimePickerDialogViewModel : INotifyPropertyChanged
    {
        private DateTime _selectedTime;
        private DateTime _minTime;
        public int SelectedHour
        {
            get => _selectedTime.Hour % 12 == 0 ? 12 : _selectedTime.Hour % 12;
            set
            {
                int hour = value % 12;
                if (SelectedPeriod == "PM") hour += 12;
                int minute = _selectedTime.Minute;
                var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, minute, 0);
                if (newTime < _minTime) newTime = _minTime;
                _selectedTime = newTime;
                NotifyAllTimeProperties();
            }
        }
        public int SelectedMinute
        {
            get => _selectedTime.Minute;
            set
            {
                int hour = _selectedTime.Hour;
                int minute = value;
                if (minute < 0) minute = 0;
                if (minute > 59) minute = 59;
                var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, minute, 0);
                if (newTime < _minTime) newTime = _minTime;
                _selectedTime = newTime;
                NotifyAllTimeProperties();
            }
        }
        public string SelectedPeriod
        {
            get => _selectedTime.Hour >= 12 ? "PM" : "AM";
            set
            {
                if (value == "AM" && _selectedTime.Hour >= 12)
                    _selectedTime = _selectedTime.AddHours(-12);
                else if (value == "PM" && _selectedTime.Hour < 12)
                    _selectedTime = _selectedTime.AddHours(12);
                if (_selectedTime < _minTime) _selectedTime = _minTime;
                OnPropertyChanged();
            }
        }
        public int PreviousHour => (SelectedHour == 1 ? 12 : SelectedHour - 1);
        public int NextHour => (SelectedHour == 12 ? 1 : SelectedHour + 1);
        public int PreviousMinute => (SelectedMinute == 0 ? 59 : SelectedMinute - 1);
        public int NextMinute => (SelectedMinute == 59 ? 0 : SelectedMinute + 1);
        public string NextPeriod => SelectedPeriod == "AM" ? "PM" : "AM";
        public string PreviousPeriod => SelectedPeriod == "AM" ? "PM" : "AM";
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand IncrementHourCommand { get; }
        public ICommand DecrementHourCommand { get; }
        public ICommand IncrementMinuteCommand { get; }
        public ICommand DecrementMinuteCommand { get; }
        public ICommand TogglePeriodCommand { get; }
        public TimePickerDialogViewModel(DateTime? initialTime = null)
        {
            _minTime = DateTime.Now;
            _selectedTime = initialTime ?? DateTime.Now;
            SaveCommand = new RelayCommand(Save, CanSave);
            CancelCommand = new RelayCommand(Cancel);
            IncrementHourCommand = new RelayCommand(IncrementHour);
            DecrementHourCommand = new RelayCommand(DecrementHour);
            IncrementMinuteCommand = new RelayCommand(IncrementMinute);
            DecrementMinuteCommand = new RelayCommand(DecrementMinute);
            TogglePeriodCommand = new RelayCommand(TogglePeriod);
        }
        private void Save()
        {
            if (_selectedTime < _minTime)
            {
                System.Windows.MessageBox.Show($"Selected time cannot be before the current time.", "Invalid Time", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }
            DialogHost.CloseDialogCommand.Execute(_selectedTime, null);
        }
        private bool CanSave()
        {
            return _selectedTime >= _minTime;
        }
        private void Cancel()
        {
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
        private void IncrementHour()
        {
            int hour = _selectedTime.Hour;
            hour = (hour + 1) % 24;
            var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, _selectedTime.Minute, 0);
            if (newTime < _minTime) newTime = _minTime;
            _selectedTime = newTime;
            NotifyAllTimeProperties();
        }
        private void DecrementHour()
        {
            int hour = _selectedTime.Hour;
            hour = (hour - 1 + 24) % 24;
            var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, _selectedTime.Minute, 0);
            if (newTime < _minTime) newTime = _minTime;
            _selectedTime = newTime;
            NotifyAllTimeProperties();
        }
        private void IncrementMinute()
        {
            int minute = _selectedTime.Minute;
            minute = (minute + 1) % 60;
            int hour = _selectedTime.Hour; // Keep the current hour, don't auto-increment
            var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, minute, 0);
            if (newTime < _minTime) newTime = _minTime;
            _selectedTime = newTime;
            NotifyAllTimeProperties();
        }
        private void DecrementMinute()
        {
            int minute = _selectedTime.Minute;
            int hour = _selectedTime.Hour; // Keep the current hour, don't auto-decrement
            if (minute == 0)
            {
                minute = 59; // Just wrap to 59, don't change hour
            }
            else
            {
                minute--;
            }
            var newTime = new DateTime(_selectedTime.Year, _selectedTime.Month, _selectedTime.Day, hour, minute, 0);
            if (newTime < _minTime) newTime = _minTime;
            _selectedTime = newTime;
            NotifyAllTimeProperties();
        }
        private void TogglePeriod()
        {
            var newTime = _selectedTime.AddHours(_selectedTime.Hour >= 12 ? -12 : 12);
            if (newTime < _minTime) newTime = _minTime;
            _selectedTime = newTime;
            NotifyAllTimeProperties();
        }
        private void NotifyAllTimeProperties()
        {
            OnPropertyChanged(nameof(SelectedHour));
            OnPropertyChanged(nameof(SelectedMinute));
            OnPropertyChanged(nameof(SelectedPeriod));
            OnPropertyChanged(nameof(PreviousHour));
            OnPropertyChanged(nameof(NextHour));
            OnPropertyChanged(nameof(PreviousMinute));
            OnPropertyChanged(nameof(NextMinute));
            OnPropertyChanged(nameof(NextPeriod));
            OnPropertyChanged(nameof(PreviousPeriod));
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
} 