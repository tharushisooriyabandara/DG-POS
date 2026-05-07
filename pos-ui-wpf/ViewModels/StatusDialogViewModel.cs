using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public enum StatusVariant
    {
        Success,
        Error,
        Info,
        Warning
    }

    public class StatusDialogViewModel : BaseViewModel
    {
        private string _header;
        public string Header
        {
            get => _header;
            set { _header = value; OnPropertyChanged(nameof(Header)); }
        }

        private string _message;
        public string Message
        {
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); }
        }

        private StatusVariant _variant;
        public StatusVariant Variant
        {
            get => _variant;
            set { _variant = value; OnPropertyChanged(nameof(Variant)); OnPropertyChanged(nameof(VariantBrush)); OnPropertyChanged(nameof(IconGlyph)); }
        }

        public Brush VariantBrush
        {
            get
            {
                switch (Variant)
                {
                    case StatusVariant.Error:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E53935"));
                    case StatusVariant.Info:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#379AE6"));
                    case StatusVariant.Warning:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA000"));
                    case StatusVariant.Success:
                    default:
                        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                }
            }
        }

        public string IconGlyph
        {
            get
            {
                switch (Variant)
                {
                    case StatusVariant.Error:
                        return "✕";
                    case StatusVariant.Info:
                        return "i";
                    case StatusVariant.Warning:
                        return "!";
                    case StatusVariant.Success:
                    default:
                        return "✓";
                }
            }
        }

        public ICommand CloseCommand { get; }

        public StatusDialogViewModel()
        {
            CloseCommand = DialogHost.CloseDialogCommand;
        }

        public static StatusDialogViewModel CreateSuccess(string header, string message)
        {
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Success
            };
        }

        // Success dialog with cash breakdown
        public static StatusDialogViewModel CreateCashSuccess(string header, decimal cashGiven, decimal totalAmount, decimal cashBalance, string orderKind, string displayOrderId)
        {
            var message = $"Cash Given - {cashGiven:0.00}\nTotal Amount - {totalAmount:0.00}\nCash Balance - {cashBalance:0.00}\n\n{orderKind} order {displayOrderId} has been placed successfully.";
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Success
            };
        }

         // Success dialog with when Completed Payment
        public static StatusDialogViewModel CreateCompletedPaymentSuccess(string header, decimal cashGiven, decimal totalAmount, decimal cashBalance, string displayOrderId)
        {
            var message = $"Cash Given - {cashGiven:0.00}\nTotal Amount - {totalAmount:0.00}\nCash Balance - {cashBalance:0.00}\n\nOrder {displayOrderId} completed successfully.";
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Success
            };
        }

        public static StatusDialogViewModel CreateError(string header, string message)
        {
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Error
            };
        }

        public static StatusDialogViewModel CreateInfo(string header, string message)
        {
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Info
            };
        }

        public static StatusDialogViewModel CreateWarning(string header, string message)
        {
            return new StatusDialogViewModel
            {
                Header = header,
                Message = message,
                Variant = StatusVariant.Warning
            };
        }

        public static StatusDialogViewModel CreateCannotEndShiftIncompleteOrders(int incompleteCount, IEnumerable<string> orderIds)
        {
            var orderWord = incompleteCount == 1 ? "order" : "orders";
            var list = orderIds?.Where(s => !string.IsNullOrWhiteSpace(s)).ToList() ?? new List<string>();
            string line1;
            if (list.Count == 0)
            {
                line1 = $"There are {incompleteCount} incomplete {orderWord}.";
            }
            else
            {
                var part = string.Join(", ", list.Take(3));
                if (list.Count > 3 || incompleteCount > list.Count)
                {
                    part += ", ...";
                }
                line1 = $"There are {incompleteCount} incomplete {orderWord} ({part}).";
            }

            const string line2 = "Please complete or cancel all pending orders before ending the shift.";
            var message = line1 + "\n" + line2;
            return new StatusDialogViewModel
            {
                Header = "Cannot End Shift",
                Message = message,
                Variant = StatusVariant.Error
            };
        }
    }
}


