using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    public class CustomPriceDialogViewModel : INotifyPropertyChanged
    {
        private readonly RelayCommand _confirmCommand;
        private string _priceInput = string.Empty;

        public CustomPriceDialogViewModel(string itemName, decimal? initialPrice = null, string dialogIdentifier = "AddItemDialogHost")
        {
            ItemName = itemName;
            DialogIdentifier = dialogIdentifier;
            CurrencySymbol = GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";

            _confirmCommand = new RelayCommand(Confirm, CanConfirm);
            CancelCommand = new RelayCommand(Cancel);
            ClearCommand = new RelayCommand(Clear);
            NumberPadCommand = new RelayCommand<string>(HandleNumberPadInput);

            if (initialPrice.HasValue && initialPrice.Value > 0)
            {
                PriceInput = initialPrice.Value.ToString("0.##");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string ItemName { get; }

        public string DialogIdentifier { get; }

        public string CurrencySymbol { get; }

        public ICommand ConfirmCommand => _confirmCommand;

        public ICommand CancelCommand { get; }

        public ICommand ClearCommand { get; }

        public ICommand NumberPadCommand { get; }

        public string PriceInput
        {
            get => _priceInput;
            private set
            {
                if (_priceInput != value)
                {
                    _priceInput = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ParsedPrice));
                    OnPropertyChanged(nameof(DisplayPrice));
                    _confirmCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public decimal? ParsedPrice
        {
            get
            {
                if (decimal.TryParse(PriceInput, out var price))
                {
                    return Math.Round(price, 2, MidpointRounding.AwayFromZero);
                }

                return null;
            }
        }

        public string DisplayPrice
        {
            get
            {
                var displayValue = string.IsNullOrWhiteSpace(PriceInput) ? "0" : PriceInput;
                return $"{CurrencySymbol} {displayValue}";
            }
        }

        private void HandleNumberPadInput(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            switch (input)
            {
                case "Backspace":
                    if (PriceInput.Length > 0)
                    {
                        PriceInput = PriceInput.Substring(0, PriceInput.Length - 1);
                    }
                    break;
                case ".":
                    if (!PriceInput.Contains("."))
                    {
                        PriceInput = string.IsNullOrEmpty(PriceInput) ? "0." : PriceInput + ".";
                    }
                    break;
                default:
                    if (input.All(char.IsDigit))
                    {
                        if (PriceInput.Contains('.'))
                        {
                            var parts = PriceInput.Split('.');
                            if (parts.Length > 1 && parts[1].Length >= 2)
                            {
                                return;
                            }
                        }
                        PriceInput += input;
                    }
                    break;
            }
        }

        private bool CanConfirm()
        {
            return ParsedPrice.HasValue && ParsedPrice.Value > 0;
        }

        private void Confirm()
        {
            if (ParsedPrice.HasValue)
            {
                DialogHost.Close(DialogIdentifier, ParsedPrice.Value);
            }
        }

        private void Cancel()
        {
            DialogHost.Close(DialogIdentifier, null);
        }

        private void Clear()
        {
            PriceInput = string.Empty;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

