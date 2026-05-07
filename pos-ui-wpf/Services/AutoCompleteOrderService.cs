using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using POS_UI.Models;
using POS_UI.ViewModels;

namespace POS_UI.Services
{
    /// <summary>
    /// When platform id or platformid2 is 9 (POS), and order config has is_takeaway/is_dinein/is_delivery true with timer mins,
    /// paid orders are auto-completed after created_at + timer (e.g. 10:05 + 5 mins -> complete at 10:10).
    /// </summary>
    public class AutoCompleteOrderService
    {
        public static AutoCompleteOrderService Instance { get; } = new AutoCompleteOrderService();

        private readonly ApiService _apiService = new ApiService();
        private DispatcherTimer _timer;
        private bool _isRunning;
        private readonly object _runLock = new object();

        private const int CheckIntervalSeconds = 60; // Run every minute

        private AutoCompleteOrderService() { }

        /// <summary>Start the background timer. Call once after login when shop is loaded.</summary>
        public void Start()
        {
            lock (_runLock)
            {
                if (_isRunning) return;
                _isRunning = true;
            }
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(CheckIntervalSeconds)
            };
            _timer.Tick += async (s, e) => await CheckAndCompleteOrdersAsync();
            _timer.Start();
            _ = CheckAndCompleteOrdersAsync(); // Run once immediately
            System.Diagnostics.Debug.WriteLine("[AutoCompleteOrderService] Started.");
        }

        public void Stop()
        {
            lock (_runLock)
            {
                if (!_isRunning) return;
                _isRunning = false;
            }
            _timer?.Stop();
            _timer = null;
            System.Diagnostics.Debug.WriteLine("[AutoCompleteOrderService] Stopped.");
        }

        private async Task CheckAndCompleteOrdersAsync()
        {
            try
            {
                var gds = GlobalDataService.Instance;
                if (!gds.UseLiveOrdersPage) return; // Only run auto-complete when Live Orders page is enabled (is_live_orders_page == true)
                bool anyEnabled = gds.IsTakeawayAutoCompleteEnabled || gds.IsDineInAutoCompleteEnabled || gds.IsDeliveryAutoCompleteEnabled;
                if (!anyEnabled) return;

                if (gds.ShopDetails == null || gds.ShopDetails.Id <= 0) return;
                var (_, outletCode, brandIdStr) = new SettingsService().LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) || !int.TryParse(brandIdStr, out int brandId))
                    return;

                var statuses = new[] { "QUEUE", "PREPARING", "READY", "SERVED", "DELIVERED" };
                var platformIds = "9";
                var allOrders = new List<OrderModel>();
                foreach (var status in statuses)
                {
                    try
                    {
                        var list = await _apiService.GetOrdersAsync(status, platformIds);
                        allOrders.AddRange(list);
                    }
                    catch { /* skip */ }
                }

                var distinctOrders = allOrders
                    .GroupBy(o => o.ApiId)
                    .Select(g => g.First())
                    .ToList();

                var now = DateTime.Now;
                foreach (var order in distinctOrders)
                {
                    if (order.PlatformId != 9 && order.PlatformId2 != 9) continue;
                    if (!string.Equals(order.PaymentStatus?.Trim(), "PAID", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(order.ApiStatus?.Trim(), "COMPLETED", StringComparison.OrdinalIgnoreCase)) continue;

                    DateTime orderCreatedAt = order.CreatedAtActual ?? order.CreatedAt;
                    int timerMins = 0;
                    bool enabled = false;

                    if (order.OrderType == OrderType.TakeAway && gds.IsTakeawayAutoCompleteEnabled)
                    {
                        enabled = true;
                        timerMins = gds.TakeawayAutoCompleteTimerMins;
                    }
                    else if (order.OrderType == OrderType.DineIn && gds.IsDineInAutoCompleteEnabled)
                    {
                        enabled = true;
                        timerMins = gds.DineInAutoCompleteTimerMins;
                    }
                    else if (order.OrderType == OrderType.Delivery && gds.IsDeliveryAutoCompleteEnabled)
                    {
                        enabled = true;
                        timerMins = gds.DeliveryAutoCompleteTimerMins;
                    }

                    if (!enabled || timerMins <= 0) continue;

                    var deadline = orderCreatedAt.AddMinutes(timerMins);
                    if (now >= deadline)
                    {
                        try
                        {
                            await KitchenViewModel.MoveToCompletedStatic(order);
                            System.Diagnostics.Debug.WriteLine($"[AutoCompleteOrderService] Auto-completed order ApiId={order.ApiId} (type={order.OrderType}, created={orderCreatedAt:HH:mm}, timer={timerMins}min).");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[AutoCompleteOrderService] Failed to complete order {order.ApiId}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoCompleteOrderService] Check error: {ex.Message}");
            }
        }
    }
}
