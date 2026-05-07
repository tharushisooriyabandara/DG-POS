using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using MaterialDesignThemes.Wpf;

namespace POS_UI.View
{
    public partial class LiveOrdersPage : Page
    {
        public LiveOrdersPage()
        {
            InitializeComponent();
            Loaded += LiveOrdersPage_Loaded;
        }

        private void LiveOrdersPage_Loaded(object sender, RoutedEventArgs e)
        {
            ConfigureScrollViewerForTouch(OrdersScrollViewer);
            SetupFadeOverlay(OrdersScrollViewer, OrdersBottomFade);
        }

        private void SetupFadeOverlay(ScrollViewer scrollViewer, Border fadeOverlay)
        {
            if (scrollViewer == null || fadeOverlay == null) return;

            scrollViewer.ScrollChanged += (s, ev) => UpdateFadeVisibility(scrollViewer, fadeOverlay);
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                new Action(() => UpdateFadeVisibility(scrollViewer, fadeOverlay)));
        }

        private void UpdateFadeVisibility(ScrollViewer scrollViewer, Border fadeOverlay)
        {
            bool canScrollDown = scrollViewer.ScrollableHeight > 0
                && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight - 1;
            fadeOverlay.Visibility = canScrollDown ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ConfigureScrollViewerForTouch(ScrollViewer scrollViewer)
        {
            if (scrollViewer == null) return;

            try
            {
                Stylus.SetIsTapFeedbackEnabled(scrollViewer, false);
                Stylus.SetIsPressAndHoldEnabled(scrollViewer, false);
                Stylus.SetIsFlicksEnabled(scrollViewer, false);
                Stylus.SetIsTouchFeedbackEnabled(scrollViewer, false);

                bool isScrolling = false;
                Point? lastPosition = null;

                scrollViewer.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    lastPosition = e.GetPosition(scrollViewer);
                    isScrolling = false;
                };

                scrollViewer.PreviewMouseMove += (s, e) =>
                {
                    if (lastPosition.HasValue && e.LeftButton == MouseButtonState.Pressed)
                    {
                        var currentPosition = e.GetPosition(scrollViewer);
                        var delta = currentPosition.Y - lastPosition.Value.Y;

                        if (!isScrolling && Math.Abs(delta) > 20)
                        {
                            isScrolling = true;
                            scrollViewer.CaptureMouse();
                        }

                        if (isScrolling)
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - delta);
                            lastPosition = currentPosition;
                            e.Handled = true;
                        }
                    }
                };

                scrollViewer.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    if (isScrolling)
                    {
                        scrollViewer.ReleaseMouseCapture();
                        e.Handled = true;
                    }

                    lastPosition = null;
                    isScrolling = false;
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Touch] Error configuring {scrollViewer.Name}: {ex.Message}");
            }
        }

        private async void OrderCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is POS_UI.Models.OrderModel order)
            {
                try
                {
                    var dialog = new KitchenOrderDetailsDialog
                    {
                        DataContext = new POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel(order.ApiId, POS_UI.ViewModels.KitchenOrderDetailsDialogViewModel.DialogMode.LiveOrders)
                    };
                    await DialogHost.Show(dialog, "RootDialog");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error showing order details: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
