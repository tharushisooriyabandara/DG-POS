using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Media.Animation;

namespace POS_UI.View
{
    /// <summary>
    /// Interaction logic for NewOrderAlertPopup.xaml
    /// </summary>
    public partial class NewOrderAlertPopup : UserControl
    {
        private readonly MediaPlayer _alertPlayer = new MediaPlayer();

        public NewOrderAlertPopup()
        {
            InitializeComponent();
            // Ensure the alert visual state resets whenever it becomes visible again
            this.IsVisibleChanged += NewOrderAlertPopup_IsVisibleChanged;

            // Prepare alert sound
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var soundPath = System.IO.Path.Combine(baseDir, "Source", "sound.mp3");
                if (System.IO.File.Exists(soundPath))
                {
                    _alertPlayer.Open(new Uri(soundPath, UriKind.Absolute));
                    _alertPlayer.MediaEnded += (s, e) =>
                    {
                        // Loop the sound while visible
                        _alertPlayer.Position = TimeSpan.Zero;
                        _alertPlayer.Play();
                    };
                }
            }
            catch { /* ignore audio init issues */ }
        }

        private void AlertBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var storyboard = this.Resources["FadeUpAndOut"] as Storyboard;
            if (storyboard == null)
            {
                // Fallback: directly close if animation missing
                CloseAlert_Click(this, new RoutedEventArgs());
                return;
            }

            void OnCompleted(object? s, EventArgs args)
            {
                storyboard.Completed -= OnCompleted;
                CloseAlert_Click(this, new RoutedEventArgs());
            }

            storyboard.Completed += OnCompleted;
            storyboard.Begin();
        }

        private void NewOrderAlertPopup_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (this.IsVisible)
            {
                // Reset state so the popup is fully visible for the next order
                if (AlertBorder != null)
                {
                    AlertBorder.Opacity = 1.0;
                }
                if (AlertTranslate != null)
                {
                    AlertTranslate.Y = 0;
                }

                // Start alert sound if loaded
                try { _alertPlayer.Play(); } catch { /* ignore */ }
            }
            else
            {
                // Stop sound when hidden
                StopAlertSound();
            }
        }

        public async Task ShowOrderDetailsDialog()
        {
            // Hide the alert popup
            var vm = this.DataContext as POS_UI.ViewModels.CashierHomeViewModel;
            if (vm != null)
                vm.IsOrderAlertVisible = false;

            // Create a sample order details view model
            var orderVm = POS_UI.ViewModels.OrderDetailsDialogViewModel.CreateSample();
            orderVm.DialogClosed += () => MaterialDesignThemes.Wpf.DialogHost.CloseDialogCommand.Execute(null, null);

            var dialog = new POS_UI.View.OrderDetailsDialog { DataContext = orderVm };
            await MaterialDesignThemes.Wpf.DialogHost.Show(dialog, "AddItemDialogHost");
        }

        private void CloseAlert_Click(object sender, RoutedEventArgs e)
        {
            // Hide the alert and surface every CREATED order from this batch on the incoming banner row
            if (this.DataContext is POS_UI.ViewModels.CashierHomeViewModel vm)
                vm.DismissNewOrderAlertAndSurfaceAllPendingToBanner();

            // Ensure sound stops on close
            StopAlertSound();
        }

        private void StopAlertSound()
        {
            try
            {
                _alertPlayer.Stop();
                // Keep player open for next alert to start instantly
                //
            }
            catch { /* ignore */ }
        }
    }
}
