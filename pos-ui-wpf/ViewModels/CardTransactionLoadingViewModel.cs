using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace POS_UI.ViewModels
{
    public class CardTransactionLoadingViewModel : INotifyPropertyChanged
    {
        private string _statusText = "Connecting to card machine...";
        private string _progressText = "Step 1 of 3";
        private bool _isProcessing = true;

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                _isProcessing = value;
                OnPropertyChanged();
            }
        }

        public ICommand CancelCommand { get; }

        public CardTransactionLoadingViewModel()
        {
            CancelCommand = new RelayCommand(CancelTransaction);
        }

        public void UpdateStatus(string status, string progress = null)
        {
            StatusText = status;
            if (!string.IsNullOrEmpty(progress))
            {
                ProgressText = progress;
            }
        }

        public void SetProcessingComplete()
        {
            IsProcessing = false;
        }

        private void CancelTransaction()
        {
            DialogHost.CloseDialogCommand.Execute("CANCELLED", null);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 