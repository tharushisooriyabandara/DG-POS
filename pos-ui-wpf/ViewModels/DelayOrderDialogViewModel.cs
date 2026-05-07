using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    public class DelayOrderDialogViewModel : BaseViewModel
    {
        private int? _selectedMinutes;
        public ObservableCollection<int> DelayOptions { get; } = new ObservableCollection<int>
        {
            10, 20, 30, 40, 50, 60
        };

        public int? SelectedMinutes
        {
            get => _selectedMinutes;
            set { _selectedMinutes = value; OnPropertyChanged(); }
        }

        public string RemoteOrderId { get; set; }
        
        // Callback to notify parent when delay is successful
        public Action OnDelaySuccess { get; set; }

        private readonly ApiService _apiService = new ApiService();

        public ICommand ConfirmDelayCommand { get; }

        public DelayOrderDialogViewModel()
        {
            ConfirmDelayCommand = new RelayCommand(async () => await ConfirmDelayAsync());
        }

        private async System.Threading.Tasks.Task ConfirmDelayAsync()
        {
            try
            {
                if (SelectedMinutes == null || SelectedMinutes.Value <= 0)
                {
                    MessageBox.Show("Please select a delay time.");
                    return;
                }
                if (string.IsNullOrWhiteSpace(RemoteOrderId))
                {
                    MessageBox.Show("Missing remote order id.");
                    return;
                }

                var res = await _apiService.NotifyUpdateReadyTimeToDeliveryPlatformAsync(RemoteOrderId, SelectedMinutes.Value);
                if (!res.IsSuccess)
                {
                    MessageBox.Show($"Failed to update ready time: {res.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Notify parent that delay was successful
                OnDelaySuccess?.Invoke();

                DialogHost.Close("RootDialog", true);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to delay order: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

