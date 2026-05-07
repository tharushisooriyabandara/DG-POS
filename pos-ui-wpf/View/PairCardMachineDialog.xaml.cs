using System.Windows;
using POS_UI.Models;

namespace POS_UI.View
{
    public partial class PairCardMachineDialog : Window
    {
        public CardMachineModel CardMachine { get; set; }
        public string PairingCode { get; set; }
        public bool PairingSuccessful { get; set; }
        public string AuthToken { get; set; }

        public PairCardMachineDialog(CardMachineModel cardMachine)
        {
            InitializeComponent();
            CardMachine = cardMachine;
            DataContext = this;
            
            // Set focus to pairing code textbox
            Loaded += (s, e) => PairingCodeTextBox.Focus();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void PairButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PairingCode))
            {
                MessageBox.Show("Please enter a pairing code.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Attempt pairing
                var api = new Services.CardMachineApiService();
                AuthToken = await api.PairDeviceAsync(
                    CardMachine.IPAddress, 
                    CardMachine.Port, 
                    CardMachine.APIEndpoint, 
                    CardMachine.DeviceId, 
                    PairingCode);
                
                PairingSuccessful = true;
                DialogResult = true;
                Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Pairing failed: {ex.Message}", "Pairing Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 