using System.Threading.Tasks;
using Newtonsoft.Json;
using POS_UI.Models;

namespace POS_UI.Services
{
    /// <summary>
    /// Persists the current cart as <c>ongoing_order</c> in terminal order config when temp payments exist for the display order id
    /// (same rules as sidebar logout).
    /// </summary>
    internal static class OngoingOrderConfigPersistence
    {
        public static async Task TrySaveFromCartAsync()
        {
            try
            {
                var cart = CartService.Instance;
                if (cart?.OrderItems == null || cart.OrderItems.Count == 0)
                    return;

                var displayOrderId = (cart.DisplayOrderId ?? cart.CashierSessionDisplayOrderId ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(displayOrderId))
                    return;

                var orderModel = OrderModel.FromCartService(
                    cart,
                    displayOrderId,
                    null,
                    cart.DiscountPercent,
                    null);

                var requestPayload = orderModel.ToApiRequest();

                var settingsService = new SettingsService();
                var (_, outletCode, brandIdStr) = settingsService.LoadSettings();
                if (string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr) ||
                    !int.TryParse(brandIdStr, out int brandId))
                    return;

                var api = new ApiService();
                var tempPayments = await api.GetTempPaymentsByDisplayOrderIdAsync(displayOrderId).ConfigureAwait(false);
                if (tempPayments.Data == null || tempPayments.Data.Count == 0)
                    return;

                var shopDetails = await api.GetShopDetailsAsync(outletCode, brandIdStr).ConfigureAwait(false);
                if (shopDetails == null || shopDetails.Id <= 0)
                    return;

                var gds = GlobalDataService.Instance;
                var orderConfig = new
                {
                    page_name = gds?.UseLiveOrdersPage == true ? "live_orders" : "orders",
                    is_live_orders_page = gds?.UseLiveOrdersPage == true,
                    is_takeaway = gds?.IsTakeawayAutoCompleteEnabled ?? false,
                    takeaway_timer_mins = gds != null ? gds.TakeawayAutoCompleteTimerMins : 0,
                    is_dinein = gds?.IsDineInAutoCompleteEnabled ?? false,
                    dinein_timer_mins = gds != null ? gds.DineInAutoCompleteTimerMins : 0,
                    is_delivery = gds?.IsDeliveryAutoCompleteEnabled ?? false,
                    delivery_timer_mins = gds != null ? gds.DeliveryAutoCompleteTimerMins : 0,
                    idle_logout_minutes = gds?.IdleLogoutMinutes ?? 10,
                    display_order_id = displayOrderId,
                    ongoing_order = new[] { requestPayload }
                };

                var orderConfigJson = JsonConvert.SerializeObject(orderConfig);
                await api.SaveOrderConfigAsync(shopDetails.Id, brandId, orderConfigJson).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort: do not block logout or application shutdown.
            }
        }
    }
}
