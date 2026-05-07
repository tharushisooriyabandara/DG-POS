using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class NumericInputDialogViewModel : INotifyPropertyChanged
    {
        private string _input = "";
        private readonly decimal _maxValue;
        private readonly string _suffix;

        public NumericInputDialogViewModel(string title, decimal? initialValue = null, decimal maxValue = 100, string suffix = "%", string dialogId = "RootDialog")
        {
            Title = title;
            _maxValue = maxValue;
            _suffix = suffix;
            DialogIdentifier = dialogId;

            NumberPadCommand = new RelayCommand<string>(HandleInput);
            ClearCommand = new RelayCommand(() => { _input = ""; Notify(); });
            ConfirmCommand = new RelayCommand(() => DialogHost.Close(DialogIdentifier, ApplyValue));
            CancelCommand = new RelayCommand(() => DialogHost.Close(DialogIdentifier, null));

            if (initialValue.HasValue && initialValue.Value > 0)
                _input = initialValue.Value.ToString("G29");
        }

        public string Title { get; }
        public string DialogIdentifier { get; }
        public ICommand NumberPadCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }

        public string DisplayValue => string.IsNullOrEmpty(_input) ? $"0 {_suffix}" : $"{_input} {_suffix}";

        public decimal? ParsedValue =>
            decimal.TryParse(_input, out var v) && v > 0 && v <= _maxValue ? v : (decimal?)null;

        public string ApplyValue => string.IsNullOrWhiteSpace(_input) ? "" : (decimal.TryParse(_input, out var v) && v >= 0 && v <= _maxValue ? v.ToString("G29") : "");

        private void HandleInput(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            switch (key)
            {
                case "Backspace":
                    if (_input.Length > 0) _input = _input[..^1];
                    break;
                case ".":
                    if (!_input.Contains('.'))
                        _input = string.IsNullOrEmpty(_input) ? "0." : _input + ".";
                    break;
                default:
                    if (key.All(char.IsDigit))
                    {
                        if (_input.Contains('.'))
                        {
                            var parts = _input.Split('.');
                            if (parts.Length > 1 && parts[1].Length >= 2) return;
                        }
                        var candidate = _input + key;
                        if (decimal.TryParse(candidate, out var val) && val > _maxValue) return;
                        _input = candidate;
                    }
                    break;
            }
            Notify();
        }

        private void Notify()
        {
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(ParsedValue));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
