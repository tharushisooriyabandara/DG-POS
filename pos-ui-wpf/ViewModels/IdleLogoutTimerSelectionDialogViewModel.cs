using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class IdleLogoutTimerSelectionDialogViewModel : INotifyPropertyChanged
    {
        public const string Option5 = "5";
        public const string Option10 = "10";
        public const string Option30 = "30";
        public const string OptionCustom = "Custom";

        private string _selectedOption = Option5;
        private int _customMinutes = 15;
        private readonly string _dialogHostId;

        public IdleLogoutTimerSelectionDialogViewModel(int initialMinutes = 10, string dialogHostId = "RootDialog")
        {
            _dialogHostId = dialogHostId;
            if (initialMinutes == 5) _selectedOption = Option5;
            else if (initialMinutes == 10) _selectedOption = Option10;
            else if (initialMinutes == 30) _selectedOption = Option30;
            else { _selectedOption = OptionCustom; _customMinutes = Math.Clamp(initialMinutes, 1, 120); }
            ConfirmCommand = new RelayCommand(Confirm, CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
            IncrementCustomCommand = new RelayCommand(() => CustomMinutes = CustomMinutes + 1);
            DecrementCustomCommand = new RelayCommand(() => CustomMinutes = CustomMinutes - 1);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Title => "Set idle logout timer";

        public string SelectedOption
        {
            get => _selectedOption;
            set
            {
                if (_selectedOption == value) return;
                _selectedOption = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCustomSelected));
                ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            }
        }

        public bool IsCustomSelected => SelectedOption == OptionCustom;

        public int CustomMinutes
        {
            get => _customMinutes;
            set
            {
                int v = Math.Clamp(value, 1, 120);
                if (_customMinutes == v) return;
                _customMinutes = v;
                OnPropertyChanged();
                ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand IncrementCustomCommand { get; }
        public ICommand DecrementCustomCommand { get; }

        private int GetResultMinutes()
        {
            return SelectedOption switch
            {
                Option5 => 5,
                Option10 => 10,
                Option30 => 30,
                OptionCustom => CustomMinutes,
                _ => 10
            };
        }

        private bool CanConfirm()
        {
            if (SelectedOption != OptionCustom) return true;
            return CustomMinutes >= 1 && CustomMinutes <= 120;
        }

        private void Confirm()
        {
            if (!CanConfirm()) return;
            DialogHost.Close(_dialogHostId, GetResultMinutes());
        }

        private void Cancel()
        {
            DialogHost.Close(_dialogHostId, null);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
