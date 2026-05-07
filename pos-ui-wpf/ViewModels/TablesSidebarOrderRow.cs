using System;
using POS_UI.Models;
using POS_UI.Services;

namespace POS_UI.ViewModels
{
    /// <summary>One order row in the Tables floor-plan sidebar (Order Details).</summary>
    public sealed class TablesSidebarOrderRow
    {
        public int OrderApiId { get; init; }
        public string DisplayOrderId { get; init; } = "";
        public string PlatformName { get; init; } = "";
        public string PlatformLogo { get; init; } = "";
        public decimal Amount { get; init; }
        public DateTime? LineDateTime { get; init; }
        public string OrderMethodDisplay { get; init; } = "";
        public string OrderStatusDisplay { get; init; } = "";
        public string CustomerName { get; init; } = "";
        public string PaymentStatusDisplay { get; init; } = "";

        public bool ShowCustomerName => !string.IsNullOrWhiteSpace(CustomerName);

        public bool ShowOrderStatusDisplay => !string.IsNullOrWhiteSpace(OrderStatusDisplay);

        public static TablesSidebarOrderRow FromOrder(OrderModel o, string? tableCustomerFallback = null)
        {
            var platform = !string.IsNullOrWhiteSpace(o.DeliveryPlatfornName)
                ? o.DeliveryPlatfornName
                : (!string.IsNullOrWhiteSpace(o.PlatformName)
                    ? o.PlatformName
                    : (!string.IsNullOrWhiteSpace(o.Platform) ? o.Platform : "POS"));

            var dt = o.DeliveryDateTime ?? o.ScheduledTime;
            if (!dt.HasValue && o.CreatedAt != default && o.CreatedAt.Year >= 2000)
            {
                dt = o.CreatedAt;
            }

            var logo = (o.PlatformLogo ?? "").Trim();
            if (string.IsNullOrWhiteSpace(logo) || string.Equals(logo, "Unknown", StringComparison.OrdinalIgnoreCase))
            {
                var pid = o.PlatformId != 0 ? o.PlatformId : o.PlatformId2;
                if (pid == 9)
                {
                    var shopLogo = GlobalDataService.Instance.ShopDetails?.ShopLogo;
                    if (!string.IsNullOrWhiteSpace(shopLogo))
                        logo = shopLogo;
                }
            }
            string method;
            if (o.IsTableOrder)
            {
                var tableMethod = (o.TableOrderMethod ?? "").Trim();
                method = string.IsNullOrWhiteSpace(tableMethod) ? "" : tableMethod;
            }
            else
            {
                method = "";
            }

            if (string.IsNullOrWhiteSpace(method))
            {
                method = (o.OrderTypeDisplay ?? "").Trim();
                if (string.IsNullOrWhiteSpace(method))
                {
                    var ship = (o.ShippingMethod ?? "").Trim();
                    method = string.IsNullOrWhiteSpace(ship)
                        ? o.OrderType switch
                        {
                            OrderType.DineIn => "DINE-IN",
                            OrderType.TakeAway => "TAKEAWAY",
                            OrderType.Delivery => "DELIVERY",
                            OrderType.Collection => "COLLECTION",
                            _ => "DINE-IN"
                        }
                        : ship.ToUpperInvariant();
                }
            }

            var status = o.LiveOrdersStatus;
            if (string.IsNullOrWhiteSpace(status))
            {
                status = !string.IsNullOrWhiteSpace(o.ApiStatus) ? o.ApiStatus : o.Status.ToString();
            }

            var customer = (o.CustomerName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(customer))
            {
                customer = (tableCustomerFallback ?? "").Trim();
            }

            var paymentStatus = (o.PaymentStatus ?? "").Trim();

            return new TablesSidebarOrderRow
            {
                OrderApiId = o.ApiId,
                DisplayOrderId = !string.IsNullOrWhiteSpace(o.DisplayOrderId)
                    ? o.DisplayOrderId
                    : (!string.IsNullOrWhiteSpace(o.OrderNumber) ? o.OrderNumber : o.ApiId.ToString()),
                PlatformName = platform,
                PlatformLogo = logo,
                Amount = o.DisplayTotal,
                LineDateTime = dt,
                OrderMethodDisplay = method.Trim().ToUpperInvariant(),
                OrderStatusDisplay = status ?? "",
                CustomerName = customer,
                PaymentStatusDisplay = paymentStatus
            };
        }

        public static TablesSidebarOrderRow FromSessionDetail(SessionOrderDetail d, SessionOrdersData? session, string? tableCustomerFallback = null)
        {
            var platform = !string.IsNullOrWhiteSpace(d.PlatformName) ? d.PlatformName : "POS";
            var dt = d.DeliveryDateTime ?? d.CreatedAt;
            var method = !string.IsNullOrWhiteSpace(d.TableOrderMethod)
                ? d.TableOrderMethod.Trim()
                : (!string.IsNullOrWhiteSpace(d.ShippingMethod) ? d.ShippingMethod.Trim() : "Dine-in");
            var status = !string.IsNullOrWhiteSpace(d.ApiStatus)
                ? d.ApiStatus
                : (!string.IsNullOrWhiteSpace(d.Status) ? d.Status : (session?.Status ?? ""));
            status = (status ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(status))
                status = status.ToUpperInvariant();

            var customer = (d.CustomerName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(customer))
            {
                customer = (tableCustomerFallback ?? "").Trim();
            }

            var paymentStatus = (!string.IsNullOrWhiteSpace(d.PaymentStatus) ? d.PaymentStatus : (session?.PaymentStatus ?? "")).Trim();
            if (paymentStatus.Length > 0)
                paymentStatus = paymentStatus.ToUpperInvariant();
            else if (d.OrderApiId > 0)
                paymentStatus = "UNPAID";

            return new TablesSidebarOrderRow
            {
                OrderApiId = d.OrderApiId,
                DisplayOrderId = !string.IsNullOrWhiteSpace(d.DisplayOrderId) ? d.DisplayOrderId : $"#{d.OrderApiId}",
                PlatformName = platform,
                PlatformLogo = d.PlatformLogo ?? "",
                Amount = d.TotalAmount,
                LineDateTime = dt,
                OrderMethodDisplay = string.IsNullOrWhiteSpace(method) ? "DINE-IN" : method.ToUpperInvariant(),
                OrderStatusDisplay = status ?? "",
                CustomerName = customer,
                PaymentStatusDisplay = paymentStatus
            };
        }
    }
}
