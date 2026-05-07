using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using POS_UI.Models;
using POS_UI.Services;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;

namespace POS_UI.Services
{
    public class ReceiptPrintingService
    {
        private static readonly ReceiptPrintingService _instance = new ReceiptPrintingService();
        public static ReceiptPrintingService Instance => _instance;

        private ReceiptPrintingService() { }

        /// <summary>
        /// Formats voucher description in the same format as cart display: "Coupon (voucher_code) - value"
        /// </summary>
        private string FormatVoucherDescription(List<VoucherModel> vouchers, string couponCode = null)
        {
            if (vouchers != null && vouchers.Count > 0)
            {
                var firstVoucher = vouchers.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.VoucherCode));
                if (firstVoucher != null)
                {
                    string voucherDescription = $"Coupon ({firstVoucher.VoucherCode})";
                    if (!string.IsNullOrEmpty(firstVoucher.VoucherValue) && decimal.TryParse(firstVoucher.VoucherValue, out decimal voucherValue))
                    {
                        if (firstVoucher.ValueType?.ToLower() == "percentage")
                        {
                            // Percentage discount
                            voucherDescription = $"Coupon ({firstVoucher.VoucherCode} {voucherValue}%)";
                        }
                        else
                        {
                            // Fixed amount discount
                            voucherDescription = $"Coupon ({firstVoucher.VoucherCode} {voucherValue:C})";
                        }
                    }
                    return voucherDescription;
                }
            }
            
            // Fallback to CouponCode if available
            if (!string.IsNullOrWhiteSpace(couponCode))
            {
                return $"Coupon ({couponCode})";
            }
            
            return "Coupon";
        }

        public List<string> GetActivePrinters()
        {
            var printers = new List<string>();
            
            try
            {
                // Use the existing PrintersService to get active printers
                var printersService = PrintersService.Instance;
                
                // Get all active printers from the service
                foreach (var printer in printersService.Printers)
                {
                    if (printer.IsActive)
                    {
                        printers.Add(printer.DeviceName);
                    }
                }
                
                if (!printers.Any())
                {
                    // No active printers found
                }
            }
            catch (Exception ex)
            {
                // Error getting printers from PrintersService
            }

            return printers;
        }

        public async Task PrintCartReceiptAsync(CartService cartService, CardTransactionResult cardTransaction = null, string paymentMethod = null)
        {
            try
            {
                var printersService = PrintersService.Instance;
                var receiptContent = GenerateCartReceiptContent(cartService, cardTransaction, paymentMethod, includeOrderPlacedLine: false);

                // Only print the cart/main-style receipt to printers with Main receipt enabled — not every active printer.
                // Otherwise kitchen printers (kitchen receipt only) also receive a full cart receipt when using the cart print button.
                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        if (!printer.IsActive)
                            continue;

                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        // Manual cart print: same rule as manual order reprint — MainReceipt only, do not require MainReceiptOnOrder.
                        if (printerSettings == null || !printerSettings.MainReceipt)
                            continue;

                        int copiesToPrint = Math.Max(1, printerSettings.MainReceiptCount);
                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Failed to print to printer
                    }
                }
            }
            catch (Exception ex)
            {
                // Error in PrintCartReceiptAsync
            }
        }

        public async Task PrintMainReceiptAsync(CartService cartService, CardTransactionResult cardTransaction = null, string paymentMethod = null)
        {
            try
            {
                // Get all printers from PrintersService
                var printersService = PrintersService.Instance;
                var receiptContent = GenerateCartReceiptContent(cartService, cardTransaction, paymentMethod);

                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        // Step 1: Check if printer is active
                        if (!printer.IsActive)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Printer {printer.DeviceName} is not active, skipping main receipt");
                            continue;
                        }

                        // Step 2: Get printer settings and check if main receipt is enabled
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null || !printerSettings.MainReceipt || !printerSettings.MainReceiptOnOrder)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Main receipt not enabled for printer {printer.DeviceName}, skipping");
                            continue;
                        }

                        // Step 3: Print the specified number of copies
                        int copiesToPrint = printerSettings.MainReceiptCount;
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Printing {copiesToPrint} copies of main receipt to {printer.DeviceName}");

                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Failed to print main receipt to {printer.DeviceName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintMainReceiptAsync: {ex.Message}");
            }
        }

        public async Task PrintKitchenReceiptAsync(CartService cartService)
        {
            try
            {
                if (cartService == null || cartService.OrderItems == null)
                {
                    return;
                }

                // Build message box content to show printer group details
                //var messageBuilder = new StringBuilder();
                //messageBuilder.AppendLine("Printer Group Details for Items:");
               // messageBuilder.AppendLine("=================================");
                //messageBuilder.AppendLine();

                // Filter items that have printer groups with assigned printers
                var itemsToPrint = new Dictionary<string, List<OrderItem>>(); // Key: printer name, Value: items to print

                foreach (var item in cartService.OrderItems)
                {
                    //messageBuilder.AppendLine($"Item: {item.DisplayName} (Qty: {item.Quantity})");
                    
                    // Check if item has printer groups
                    if (item.Product?.PrinterGroups == null || item.Product.PrinterGroups.Count == 0)
                    {
                       // messageBuilder.AppendLine("  - No Printer Groups");
                       // messageBuilder.AppendLine();
                        // Item has no printer groups, skip it
                        continue;
                    }

                    //messageBuilder.AppendLine($"  - Printer Groups ({item.Product.PrinterGroups.Count}):");
                   // foreach (var printerGroup in item.Product.PrinterGroups)
                   // {
                    //    messageBuilder.AppendLine($"    • {printerGroup.Name} (ID: {printerGroup.Id})");
                   // }

                    // Get assigned printers for this item's printer groups
                    var assignedPrinters = GetAssignedPrintersForItem(item);
                    if (assignedPrinters.Count == 0)
                    {
                        //messageBuilder.AppendLine("  - Assigned Printers: None (Item will NOT be printed)");
                        //messageBuilder.AppendLine();
                        // No printers assigned to this item's printer groups, skip it
                        continue;
                    }

                    //messageBuilder.AppendLine($"  - Assigned Printers ({assignedPrinters.Count}):");
                    //foreach (var printerName in assignedPrinters)
                    //{
                        //messageBuilder.AppendLine($"    • {printerName}");
                    //}
                    //messageBuilder.AppendLine();

                    // Add item to each assigned printer's list
                    foreach (var printerName in assignedPrinters)
                    {
                        if (!itemsToPrint.ContainsKey(printerName))
                        {
                            itemsToPrint[printerName] = new List<OrderItem>();
                        }
                        itemsToPrint[printerName].Add(item);
                    }
                }

                // Show message box with printer group details
                //System.Windows.MessageBox.Show(messageBuilder.ToString(), "Printer Group Details", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                // Print to each printer with its assigned items
                var printersService = PrintersService.Instance;
                foreach (var kvp in itemsToPrint)
                {
                    var printerName = kvp.Key;
                    var itemsForPrinter = kvp.Value;

                    try
                    {
                        var printer = printersService.Printers.FirstOrDefault(p => p.DeviceName == printerName);
                        if (printer == null || !printer.IsActive)
                        {
                            continue;
                        }

                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printerName);
                        if (printerSettings == null || !printerSettings.KitchenReceipt)
                        {
                            continue;
                        }

                        var printerGroupName = GetPrinterGroupNameForPrinter(printerName, itemsForPrinter);
                        var receiptContent = GenerateKitchenReceiptContentForItems(cartService, itemsForPrinter, printerGroupName);
                        int copiesToPrint = printerSettings.KitchenReceiptCount;
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Printing {copiesToPrint} copies of kitchen receipt to {printerName}");

                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printerName, receiptContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Failed to print kitchen receipt to {printerName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintKitchenReceiptAsync: {ex.Message}");
            }
        }

        public async Task PrintKitchenReceiptForItemsAsync(CartService cartService, IEnumerable<OrderItem> items)
        {
            try
            {
                if (cartService == null || items == null)
                {
                    return;
                }

                var itemsList = items.ToList();
                if (itemsList.Count == 0)
                {
                    return; // nothing to print
                }

                // Filter items that have printer groups with assigned printers
                var itemsToPrint = new Dictionary<string, List<OrderItem>>(); // Key: printer name, Value: items to print

                foreach (var item in itemsList)
                {
                    // Check if item has printer groups
                    if (item.Product?.PrinterGroups == null || item.Product.PrinterGroups.Count == 0)
                    {
                        // Item has no printer groups, skip it
                        continue;
                    }

                    // Get assigned printers for this item's printer groups
                    var assignedPrinters = GetAssignedPrintersForItem(item);
                    if (assignedPrinters.Count == 0)
                    {
                        // No printers assigned to this item's printer groups, skip it
                        continue;
                    }

                    // Add item to each assigned printer's list
                    foreach (var printerName in assignedPrinters)
                    {
                        if (!itemsToPrint.ContainsKey(printerName))
                        {
                            itemsToPrint[printerName] = new List<OrderItem>();
                        }
                        itemsToPrint[printerName].Add(item);
                    }
                }

                // Print to each printer with its assigned items
                var printersService = PrintersService.Instance;
                foreach (var kvp in itemsToPrint)
                {
                    var printerName = kvp.Key;
                    var itemsForPrinter = kvp.Value;

                    try
                    {
                        var printer = printersService.Printers.FirstOrDefault(p => p.DeviceName == printerName);
                        if (printer == null || !printer.IsActive)
                        {
                            continue;
                        }

                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printerName);
                        if (printerSettings == null || !printerSettings.KitchenReceipt)
                        {
                            continue;
                        }

                        var printerGroupName = GetPrinterGroupNameForPrinter(printerName, itemsForPrinter);
                        var receiptContent = GenerateKitchenReceiptContentForItems(cartService, itemsForPrinter, printerGroupName);
                        int copiesToPrint = printerSettings.KitchenReceiptCount;
                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printerName, receiptContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Failed to print kitchen receipt to {printerName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintKitchenReceiptForItemsAsync: {ex.Message}");
            }
        }

        public async Task PrintIncomingOrderReceiptAsync(OrderModel order, string paymentMethod = null)
        {
            try
            {
                // Get all printers from PrintersService
                var printersService = PrintersService.Instance;
                var receiptContent = GenerateOrderReceiptContent(order, paymentMethod);

                // For kitchen receipt, filter items by printer groups
                var kitchenItemsToPrint = new Dictionary<string, List<OrderItem>>(); // Key: printer name, Value: items to print

                if (order?.Items != null)
                {
                    foreach (var item in order.Items)
                    {
                        // Check if item has printer groups
                        if (item.Product?.PrinterGroups == null || item.Product.PrinterGroups.Count == 0)
                        {
                            // Item has no printer groups, skip it for kitchen receipt
                            continue;
                        }

                        // Get assigned printers for this item's printer groups
                        var assignedPrinters = GetAssignedPrintersForItem(item);
                        if (assignedPrinters.Count == 0)
                        {
                            // No printers assigned to this item's printer groups, skip it
                            continue;
                        }

                        // Add item to each assigned printer's list
                        foreach (var printerName in assignedPrinters)
                        {
                            if (!kitchenItemsToPrint.ContainsKey(printerName))
                            {
                                kitchenItemsToPrint[printerName] = new List<OrderItem>();
                            }
                            kitchenItemsToPrint[printerName].Add(item);
                        }
                    }
                }

                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        // Step 1: Check if printer is active
                        if (!printer.IsActive)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Printer {printer.DeviceName} is not active, skipping incoming order receipt");
                            continue;
                        }

                        // Step 2: Get printer settings and check if main receipt/kitchen receipt are enabled
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null)
                        {
                            continue;
                        }

                        // Print like Take Away rules: if both enabled, print both; otherwise print whichever is enabled
                        if (printerSettings.MainReceipt && printerSettings.MainReceiptOnOrder)
                        {
                            int copiesToPrint = printerSettings.MainReceiptCount;
                            for (int i = 0; i < copiesToPrint; i++)
                            {
                                await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                            }
                        }
                        
                        // For kitchen receipt, only print if this printer has assigned items
                        if (printerSettings.KitchenReceipt && kitchenItemsToPrint.ContainsKey(printer.DeviceName))
                        {
                            var itemsForPrinter = kitchenItemsToPrint[printer.DeviceName];
                            if (itemsForPrinter.Count > 0)
                            {
                                // Create a temporary order with only items for this printer
                                var tempOrder = new OrderModel
                                {
                                    DisplayOrderId = order.DisplayOrderId,
                                    OrderNumber = order.OrderNumber,
                                    ApiId = order.ApiId,
                                    OrderType = order.OrderType,
                                    TableName = order.TableName,
                                    DeliveryPlatfornName = order.DeliveryPlatfornName,
                                    OrderNotes = order.OrderNotes,
                                    ScheduledTime = order.ScheduledTime,
                                    DeliveryDateTime = order.DeliveryDateTime,
                                    IsFromPhpApi = order.IsFromPhpApi,
                                    PlatformId = order.PlatformId,
                                    PlatformId2 = order.PlatformId2,
                                    Platform = order.Platform,
                                    TableOrderMethod = order.TableOrderMethod,
                                    Items = itemsForPrinter
                                };

                                var printerGroupName = GetPrinterGroupNameForPrinter(printer.DeviceName, itemsForPrinter);
                                var kitchenContent = GenerateKitchenReceiptContentFromOrder(tempOrder, printerGroupName);
                                int kc = printerSettings.KitchenReceiptCount;
                                for (int i = 0; i < kc; i++)
                                {
                                    await PrintToPrinterAsync(printer.DeviceName, kitchenContent);
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Failed to print incoming order receipt to {printer.DeviceName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintIncomingOrderReceiptAsync: {ex.Message}");
            }
        }

        //If true, only print to printers that have "Print main receipt on order" enabled. If false, print to any printer with MainReceipt enabled
        public async Task PrintIncomingMainReceiptAsync(OrderModel order, string paymentMethod = null, bool onlyWhenMainReceiptOnOrder = true)
        {
            try
            {
                var printersService = PrintersService.Instance;
                var receiptContent = GenerateOrderReceiptContent(order, paymentMethod);

                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        if (!printer.IsActive) continue;
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null || !printerSettings.MainReceipt) continue;
                        if (onlyWhenMainReceiptOnOrder && !printerSettings.MainReceiptOnOrder) continue;
                        int copiesToPrint = printerSettings.MainReceiptCount;
                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public async Task PrintIncomingKitchenReceiptAsync(OrderModel order)
        {
            try
            {
                if (order == null || order.Items == null)
                {
                    return;
                }

                // Filter items that have printer groups with assigned printers
                var itemsToPrint = new Dictionary<string, List<OrderItem>>(); // Key: printer name, Value: items to print

                foreach (var item in order.Items)
                {
                    // Check if item has printer groups
                    System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Item: {item.Product?.ItemName} has {item.Product?.PrinterGroups?.Count} printer groups");
                    if (item.Product?.PrinterGroups == null || item.Product.PrinterGroups.Count == 0)
                    {
                        // Item has no printer groups, skip it
                        continue;
                    }

                    // Get assigned printers for this item's printer groups
                    var assignedPrinters = GetAssignedPrintersForItem(item);
                    if (assignedPrinters.Count == 0)
                    {
                        // No printers assigned to this item's printer groups, skip it
                        continue;
                    }

                    // Add item to each assigned printer's list
                    foreach (var printerName in assignedPrinters)
                    {
                        if (!itemsToPrint.ContainsKey(printerName))
                        {
                            itemsToPrint[printerName] = new List<OrderItem>();
                        }
                        itemsToPrint[printerName].Add(item);
                    }
                }

                // Print to each printer with its assigned items
                var printersService = PrintersService.Instance;
                foreach (var kvp in itemsToPrint)
                {
                    var printerName = kvp.Key;
                    var itemsForPrinter = kvp.Value;

                    try
                    {
                        var printer = printersService.Printers.FirstOrDefault(p => p.DeviceName == printerName);
                        if (printer == null || !printer.IsActive)
                        {
                            continue;
                        }

                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printerName);
                        if (printerSettings == null || !printerSettings.KitchenReceipt)
                        {
                            continue;
                        }

                        // Create a temporary order with only items for this printer
                        var tempOrder = new OrderModel
                        {
                            DisplayOrderId = order.DisplayOrderId,
                            OrderNumber = order.OrderNumber,
                            ApiId = order.ApiId,
                            OrderType = order.OrderType,
                            TableName = order.TableName,
                            DeliveryPlatfornName = order.DeliveryPlatfornName,
                            OrderNotes = order.OrderNotes,
                            ScheduledTime = order.ScheduledTime,
                            DeliveryDateTime = order.DeliveryDateTime,
                            IsFromPhpApi = order.IsFromPhpApi,
                            PlatformId = order.PlatformId,
                            PlatformId2 = order.PlatformId2,
                            Platform = order.Platform,
                            TableOrderMethod = order.TableOrderMethod,
                            Items = itemsForPrinter
                        };

                        var printerGroupName = GetPrinterGroupNameForPrinter(printerName, itemsForPrinter);
                        var kitchenContent = GenerateKitchenReceiptContentFromOrder(tempOrder, printerGroupName);
                        int copiesToPrint = printerSettings.KitchenReceiptCount;
                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printerName, kitchenContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Failed to print kitchen receipt to {printerName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintIncomingKitchenReceiptAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the list of printer names assigned to the item's printer groups
        /// </summary>
        private List<string> GetAssignedPrintersForItem(OrderItem item)
        {
            var assignedPrinters = new HashSet<string>();

            if (item?.Product?.PrinterGroups == null || item.Product.PrinterGroups.Count == 0)
            {
                return assignedPrinters.ToList();
            }

            foreach (var printerGroup in item.Product.PrinterGroups)
            {
                // Get selected printers for this printer group
                var selectedPrinters = PrinterGroupSelectionService.Instance.GetSelectedPrintersForGroup(printerGroup.Id);
                foreach (var printerName in selectedPrinters)
                {
                    assignedPrinters.Add(printerName);
                }
            }

            return assignedPrinters.ToList();
        }

        /// <summary>
        /// Gets the printer group name for a printer based on the items being printed.
        /// </summary>
        private string GetPrinterGroupNameForPrinter(string printerName, IEnumerable<OrderItem> items)
        {
            if (string.IsNullOrEmpty(printerName) || items == null) return null;
            foreach (var item in items)
            {
                if (item?.Product?.PrinterGroups == null) continue;
                foreach (var group in item.Product.PrinterGroups)
                {
                    var selected = PrinterGroupSelectionService.Instance.GetSelectedPrintersForGroup(group.Id);
                    if (selected != null && selected.Contains(printerName))
                        return group?.Name;
                }
            }
            return null;
        }

        public async Task PrintIncomingOrderReceiptFromJsonAsync(string orderJsonData, string selectedTableName = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(orderJsonData)) return;

                using var doc = JsonDocument.Parse(orderJsonData);
                var root = doc.RootElement;
                // Support envelope { code, message, data: { ...order... } }
                var orderElement = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object
                    ? dataEl
                    : root;
                var mappedOrder = MapJsonToOrderModel(orderElement);

                // For dine-in table orders, use the POS-selected table name and ensure table-order fields so main/kitchen receipts show correct order type and table
                if (!string.IsNullOrWhiteSpace(selectedTableName))
                {
                    mappedOrder.TableName = selectedTableName.Trim();
                    // Treat as table order so receipt shows TableOrderMethod and table name (in case JSON did not include platform_id / table_order_method)
                    if (mappedOrder.PlatformId == 0 && mappedOrder.PlatformId2 == 0)
                    {
                        mappedOrder.PlatformId = 8;
                        mappedOrder.PlatformId2 = 8;
                    }
                    if (string.IsNullOrWhiteSpace(mappedOrder.Platform))
                    {
                        mappedOrder.Platform = "Table order";
                    }
                    if (string.IsNullOrWhiteSpace(mappedOrder.TableOrderMethod))
                    {
                        mappedOrder.TableOrderMethod = "Dine-in";
                    }
                }

                // Derive payment method if available
                string paymentMethod = null;
                if (orderElement.TryGetProperty("payment_method", out var pmProp) && pmProp.ValueKind == JsonValueKind.String)
                {
                    paymentMethod = pmProp.GetString();
                }
                else if (orderElement.TryGetProperty("payment_mode", out var pModeProp) && pModeProp.ValueKind == JsonValueKind.String)
                {
                    paymentMethod = pModeProp.GetString();
                }
                else if (orderElement.TryGetProperty("payment_status", out var psProp) && psProp.ValueKind == JsonValueKind.String)
                {
                    paymentMethod = psProp.GetString();
                }

                await PrintIncomingOrderReceiptAsync(mappedOrder, paymentMethod);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error in PrintIncomingOrderReceiptFromJsonAsync: {ex.Message}");
            }
        }

        public async Task PrintCashSessionReceiptAsync(CashDrawerSessionModel session)
        {
            try
            {
                if (session == null) return;

                var printersService = PrintersService.Instance;
                var receiptContent = GenerateCashSessionReceiptContent(session);

                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        if (!printer.IsActive) continue;
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null || !printerSettings.MainReceipt) continue;
                        int copiesToPrint = Math.Max(1, printerSettings.MainReceiptCount);
                        for (int i = 0; i < copiesToPrint; i++)
                        {
                            await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private string GenerateCashSessionReceiptContent(CashDrawerSessionModel session)
        {
            var sb = new StringBuilder();

            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"**Shift {session.Id}**");
            sb.AppendLine(new string('=', 50));

            try
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                var outlet = shop?.Name;
                if (!string.IsNullOrWhiteSpace(outlet))
                {
                    sb.AppendLine(outlet);
                    sb.AppendLine(new string('-', 50));
                }
            }
            catch { }

            sb.AppendLine($"Shift ID        : {session.Id}");
            sb.AppendLine($"Opened At         : {FormatDateWithTime(session.OpenedAt)}");
            if (session.ClosedAt.HasValue)
            {
                sb.AppendLine($"Closed At         : {FormatDateWithTime(session.ClosedAt.Value)}");    
            }
            if (!string.IsNullOrWhiteSpace(session.SessionStartedUser))
            {
                sb.AppendLine($"Started By        : {session.SessionStartedUser}");
            }
            if (!string.IsNullOrWhiteSpace(session.SessionEndedUser))
            {
                sb.AppendLine($"Ended By          : {session.SessionEndedUser}");
            }
            sb.AppendLine(new string('-', 50));

            sb.AppendLine($"Opening Balance   : {session.OpeningBalance:N2}");
            sb.AppendLine($"Sales Amount      : {session.TotalSalesAmount:N2}");
            if (session.OtherSalesAmount != 0m)
            {
                sb.AppendLine($"Other Sales       : {session.OtherSalesAmount:N2}");
            }
            sb.AppendLine($"Cash In Total     : {session.TotalInAmount:N2}");
            sb.AppendLine($"Cash Out Total    : {session.TotalOutAmount:N2}");
            sb.AppendLine($"Refunds Total     : {session.TotalRefundAmount:N2}");
            sb.AppendLine(new string('-', 50));

            sb.AppendLine($"Expected Closing  : {session.ClosingBalanceExpected:N2}");
            if (session.ClosingBalanceCounted.HasValue)
            {
                sb.AppendLine($"Counted Closing   : {session.ClosingBalanceCounted.Value:N2}");
            }
            sb.AppendLine($"Difference        : {session.Difference:N2}");
            if (!string.IsNullOrWhiteSpace(session.Status))
            {
                sb.AppendLine($"Status            : {session.Status}");
            }

            sb.AppendLine(new string('=', 50));
            sb.AppendLine($"Printed At: {FormatDateWithTime(DateTime.Now)}");
            sb.AppendLine(new string('=', 50));

            return sb.ToString();
        }

        private string GenerateReportReceiptContent(CashDrawerSessionModel session, POS_UI.Models.ZReportStatsModel zReportStats = null, List<POS_UI.Models.CashDrawerSessionModel> cashSessions = null, POS_UI.Models.TaxSummaryModel taxData = null)
        {
            var sb = new StringBuilder();

            try
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                var currentUser = GlobalDataService.Instance.CurrentUser;
                var currency = shop?.Currency ?? "£";

                // Determine report type: X-Report if session is not closed, Z-Report if closed
                var reportTitle = (session.ClosedAt.HasValue) ? "**Z-REPORT**" : "**X-REPORT**";
                sb.AppendLine(reportTitle);
                sb.AppendLine(new string('=', 50)); 

                // Outlet Name (before Address)
                sb.AppendLine($"Outlet Name: {shop?.Name ?? "Unknown Outlet"}");

                // Address
                if (!string.IsNullOrWhiteSpace(shop?.Address))
                {
                    sb.AppendLine($"Address : {shop.Address}");
                }
                else
                {
                    sb.AppendLine("Address : ");
                }

                // Report Date & Time
                sb.AppendLine($"Report Date & Time : {FormatDateWithTime(DateTime.Now)}");
                //sb.AppendLine(new string('-', 50));

                // Shift ID
                sb.AppendLine($"Shift ID: {session.Id}");

                // Shift Opened
                sb.AppendLine($"Shift Opened: {FormatDateWithTime(session.OpenedAt)}");

                // Shift Closed - Only show for Z reports (when session is closed)
                if (session.ClosedAt.HasValue)
                {
                    sb.AppendLine($"Shift Closed: {FormatDateWithTime(session.ClosedAt.Value)}");
                }

                // Opened By
                if (!string.IsNullOrWhiteSpace(session.SessionStartedUser))
                {
                    sb.AppendLine($"Opened By: {session.SessionStartedUser}");
                }
                else
                {
                    sb.AppendLine("Opened By: ");
                }

                // Closed By - Only show for Z reports (when session is closed)
                if (session.ClosedAt.HasValue && !string.IsNullOrWhiteSpace(session.SessionEndedUser))
                {
                    sb.AppendLine($"Closed By: {session.SessionEndedUser}");
                }

                // Generated By
                if (currentUser != null && !string.IsNullOrWhiteSpace(currentUser.FullName))
                {
                    sb.AppendLine($"Generated By : {currentUser.FullName}");
                }
                else
                {
                    sb.AppendLine("Generated By : ");
                }

                sb.AppendLine(new string('=', 50));

                // Sales Breakdown Section
                if (zReportStats != null)
                {
                    sb.AppendLine("**Gross Sales Breakdown**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Define column widths for table (total width ~48 chars for 80mm printer)
                    int col1Width = 22; // Category column
                    int col2Width = 8;  // Orders column
                    int col3Width = 18; // Gross Sales column
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row
                    sb.AppendLine(FormatTableRow("Category", "Orders", "Sales (" + currency + ")", col1Width, col2Width, col3Width));
                    sb.AppendLine(new string('-', 50));

                    // Table rows
                    sb.AppendLine(FormatTableRow("Takeaway Orders (POS)", zReportStats.TakeawayOrderCount.ToString(), $"{currency} {zReportStats.TakeawayRevenue:F2}", col1Width, col2Width, col3Width));
                    sb.AppendLine(FormatTableRow("Dine-In Orders (POS)", zReportStats.DineInOrderCount.ToString(), $"{currency} {zReportStats.DineInRevenue:F2}", col1Width, col2Width, col3Width));
                    sb.AppendLine(FormatTableRow("Delivery Orders (POS)", zReportStats.PosDeliveryOrderCount.ToString(), $"{currency} {zReportStats.PosDeliveryRevenue:F2}", col1Width, col2Width, col3Width));
                    
                    // Display individual platforms where PlatformId != 9
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var platformName = kvp.Key;
                                var collectionOrders = platform.GrossSales?.CollectionOrders ?? 0;
                                var deliveryOrders = platform.GrossSales?.DeliveryOrders ?? 0;
                                var totalOrders = collectionOrders + deliveryOrders;
                                
                                var collectionRevenue = ParseCurrencyString(platform.GrossSales?.CollectionRevenue);
                                var deliveryRevenue = ParseCurrencyString(platform.GrossSales?.DeliveryOrderRevenue);
                                var totalRevenue = collectionRevenue + deliveryRevenue;
                                
                                // Handle long platform names with text wrapping
                                var wrappedLines = WrapPlatformName(platformName, col1Width);
                                if (wrappedLines.Count > 0)
                                {
                                    // First line: platform name (first part) + orders + revenue
                                    sb.AppendLine(FormatTableRow(wrappedLines[0], totalOrders.ToString(), $"{currency} {totalRevenue:F2}", col1Width, col2Width, col3Width));
                                    
                                    // Additional lines: wrapped platform name parts only (empty orders and revenue columns)
                                    for (int i = 1; i < wrappedLines.Count; i++)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", col1Width, col2Width, col3Width));
                                    }
                                }
                            }
                        }
                    }
                    
                    //sb.AppendLine(new string('-', 50));
                    
                    // Total Gross Sales (bold)
                    sb.AppendLine(FormatTableRow("Total Gross Sales", $"{zReportStats.TotalOrderCount}", $"{currency} {zReportStats.TotalRevenue:F2}", col1Width, col2Width, col3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));

                    // Tender Summary Section
                    sb.AppendLine("**Tender Summary POS and Webshops**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Use same column widths as Sales Breakdown
                    int tenderCol1Width = 25; // Tender Type column
                    int tenderCol2Width = 8;  // Orders column
                    int tenderCol3Width = 12; // Sales column
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row
                    sb.AppendLine(FormatTableRow("Tender Type", "Orders", "Sales (" + currency + ")", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                    sb.AppendLine(new string('-', 50));

                    sb.AppendLine(FormatTableRow("Cash Sales", "", "", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                    
                    // Cash Sales - POS (sub-row)
                    sb.AppendLine(FormatTableRow(" -POS", zReportStats.PosCashOrderCount.ToString(), $"{currency} {zReportStats.PosCashSales:F2}", tenderCol1Width, tenderCol2Width, tenderCol3Width));

                    // Display individual platforms where PlatformId != 9
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var platformName = "-" + kvp.Key;
                                var cashOrderCount = platform.TenderSummary?.CashOrderCount ?? 0;
                                var cashRevenue = ParseCurrencyString(platform.TenderSummary?.CashRevenue);
                                
                                // Handle long platform names with text wrapping
                                var wrappedLines = WrapPlatformName(platformName, tenderCol1Width);
                                if (wrappedLines.Count > 0)
                                {
                                    // First line: platform name (first part) + orders + revenue
                                    sb.AppendLine(FormatTableRow(wrappedLines[0], cashOrderCount.ToString(), $"{currency} {cashRevenue:F2}", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                                    
                                    // Additional lines: wrapped platform name parts only (empty orders and revenue columns)
                                    for (int i = 1; i < wrappedLines.Count; i++)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                                    }
                                }
                            }
                        }
                    }

                    // Check if there are any platforms with card sales before showing Card Sales heading
                    bool hasCardSales = false;
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var cardOnlineOrderCount = platform.TenderSummary?.CardOnlineOrderCount ?? 0;
                                var cardOnlineRevenue = ParseCurrencyString(platform.TenderSummary?.CardOnlineRevenue);
                                
                                // Check if there are orders or revenue
                                if (cardOnlineOrderCount > 0 || cardOnlineRevenue > 0m)
                                {
                                    hasCardSales = true;
                                    break;
                                }
                            }
                        }
                    }

                    // Card Sales subheading - only show if there are platforms with card sales
                    if (hasCardSales)
                    {
                        sb.AppendLine(FormatTableRow("Card Sales (Online)", "", "", tenderCol1Width, tenderCol2Width, tenderCol3Width));

                        // Display individual platforms where PlatformId != 9 for Card Sales
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var platformName = "-" + kvp.Key;
                                var cardOnlineOrderCount = platform.TenderSummary?.CardOnlineOrderCount ?? 0;
                                var cardOnlineRevenue = ParseCurrencyString(platform.TenderSummary?.CardOnlineRevenue);
                                
                                // Only display if there are orders or revenue
                                if (cardOnlineOrderCount > 0 || cardOnlineRevenue > 0m)
                                {
                                    // Handle long platform names with text wrapping
                                    var wrappedLines = WrapPlatformName(platformName, tenderCol1Width);
                                    if (wrappedLines.Count > 0)
                                    {
                                        // First line: platform name (first part) + orders + revenue
                                        sb.AppendLine(FormatTableRow(wrappedLines[0], cardOnlineOrderCount.ToString(), $"{currency} {cardOnlineRevenue:F2}", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                                        
                                        // Additional lines: wrapped platform name parts only (empty orders and revenue columns)
                                        for (int i = 1; i < wrappedLines.Count; i++)
                                        {
                                            sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Card (Machine-POS) - Total
                    sb.AppendLine(FormatTableRow("Card (Machine-POS)", zReportStats.CardMachineOrderCount.ToString(), $"{currency} {zReportStats.CardMachineSales:F2}", tenderCol1Width, tenderCol2Width, tenderCol3Width));

                    // Other / Gift Card / Voucher
                    sb.AppendLine(FormatTableRow("Other/Gift Card/Voucher", "-", "-", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                    
                    // Total Tenders (Sales Only)
                    sb.AppendLine(FormatTableRow("Total Tenders", $"{zReportStats.TotalTenderOrderCount}", $"{currency} {zReportStats.TotalTenderSales:F2}", tenderCol1Width, tenderCol2Width, tenderCol3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));

                    // Refund Summary Section (mirrors Tender Summary: Cash refunds / Card refunds with POS and platform rows)
                    sb.AppendLine("**Refund Summary**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Define column widths for refund table (3 columns, match Tender Summary)
                    int refundCol1Width = 25;
                    int refundCol2Width = 8;
                    int refundCol3Width = 12;
                    
                    sb.AppendLine(":TABLE11:");
                    sb.AppendLine(FormatTableRow("Refund Type", "", "Amount (" + currency + ")", refundCol1Width, refundCol2Width, refundCol3Width));
                    sb.AppendLine(new string('-', 50));
                    
                    // Cash refunds subheading
                    sb.AppendLine(FormatTableRow("Cash refunds", "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                    // POS Cash refunds
                    sb.AppendLine(FormatTableRow(" -POS", "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                    sb.AppendLine(FormatTableRow("  --Cash Sale Cash refund", "", $"{currency} {zReportStats.PosCashSaleCashRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                    sb.AppendLine(FormatTableRow("  --Card Sale Cash refund", "", $"{currency} {zReportStats.PosCardSaleCashRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                    // Other platform cash refunds (PlatformId != 9, same as Tender Summary)
                    decimal platformCashRefundTotal = 0m;
                    decimal platformCashSaleCashRefundTotal = 0m;
                    decimal platformCardSaleCashRefundTotal = 0m;
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            if (platform.PlatformId != 9)
                            {
                                var cashRefund = ParseCurrencyString(platform.RefundSummary?.CashRefund);
                                var cashSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CashSaleCashRefund);
                                var cardSaleCashRefund = ParseCurrencyString(platform.RefundSummary?.CardSaleCashRefund);
                                if (cashRefund > 0m || cashSaleCashRefund > 0m || cardSaleCashRefund > 0m)
                                {
                                    platformCashRefundTotal += cashRefund;
                                    platformCashSaleCashRefundTotal += cashSaleCashRefund;
                                    platformCardSaleCashRefundTotal += cardSaleCashRefund;
                                    var platformName = "-" + kvp.Key;
                                    var wrappedLines = WrapPlatformName(platformName, refundCol1Width);
                                    if (wrappedLines.Count > 0)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[0], "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                                        for (int i = 1; i < wrappedLines.Count; i++)
                                            sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                                    }
                                    sb.AppendLine(FormatTableRow("  --Cash Sale Cash refund", "", $"{currency} {cashSaleCashRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                                    sb.AppendLine(FormatTableRow("  --Card Sale Cash refund", "", $"{currency} {cardSaleCashRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                                }
                            }
                        }
                    }
                    
                    // Card refunds subheading
                    sb.AppendLine(FormatTableRow("Card refunds", "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                    // POS Card refunds
                    sb.AppendLine(FormatTableRow(" -POS", "", $"{currency} {zReportStats.PosCardRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                    // Other platform card refunds (CardRefund only per platform, PlatformId != 9)
                    decimal platformCardRefundTotal = 0m;
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            if (platform.PlatformId != 9)
                            {
                                var cardRefund = ParseCurrencyString(platform.RefundSummary?.CardRefund);
                                if (cardRefund > 0m)
                                {
                                    platformCardRefundTotal += cardRefund;
                                    var platformName = "-" + kvp.Key;
                                    var wrappedLines = WrapPlatformName(platformName, refundCol1Width);
                                    if (wrappedLines.Count > 0)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[0], "", $"{currency} {cardRefund:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                                        for (int i = 1; i < wrappedLines.Count; i++)
                                            sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", refundCol1Width, refundCol2Width, refundCol3Width));
                                    }
                                }
                            }
                        }
                    }
                    
                    // Total Refunds (POS + platforms: cash refund, card refund, Cash Sale Cash refund, Card Sale Cash refund)
                    decimal totalRefundsDisplayed = zReportStats.PosCardRefund
                        + zReportStats.PosCashSaleCashRefund + zReportStats.PosCardSaleCashRefund
                        + platformCardRefundTotal
                        + platformCashSaleCashRefundTotal + platformCardSaleCashRefundTotal;
                    sb.AppendLine(FormatTableRow("Total Refunds", "", $"{currency} {totalRefundsDisplayed:F2}", refundCol1Width, refundCol2Width, refundCol3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));

                    // Voids & Cancelled Orders Section
                  /*  sb.AppendLine("**Voids & Cancelled Orders**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Define column widths for cancelled orders table (3 columns, using FormatTableRow for consistency)
                    // Use empty middle column to maintain 3-column structure like other sections
                    int cancelledCol1Width = 28; // Type column (match Tender Summary col1 width)
                    int cancelledCol2Width = 8;  // Empty middle column (for consistency)
                    int cancelledCol3Width = 12; // Amount column (match Tender Summary col3 width)
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row - use FormatTableRow for consistent alignment
                    sb.AppendLine(FormatTableRow("Type", "", "Amount (" + currency + ")", cancelledCol1Width, cancelledCol2Width, cancelledCol3Width));
                    sb.AppendLine(new string('-', 50));
                    
                    // Table rows - use FormatTableRow with empty middle column
                    sb.AppendLine(FormatTableRow("POS", "", $"{currency} {zReportStats.PosVoidSales:F2}", cancelledCol1Width, cancelledCol2Width, cancelledCol3Width));
                    sb.AppendLine(FormatTableRow("Platform Orders", "", $"{currency} {zReportStats.PlatformVoidSales:F2}", cancelledCol1Width, cancelledCol2Width, cancelledCol3Width));
                    
                    // Total Cancelled
                    sb.AppendLine(FormatTableRow("Total Cancelled", "", $"{currency} {zReportStats.TotalVoidSales:F2}", cancelledCol1Width, cancelledCol2Width, cancelledCol3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));
*/
                    // Adjustments & Discounts Section
                    sb.AppendLine("**Adjustments & Discounts**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Define column widths for discounts table (3 columns) - match Sales Breakdown style
                    int discountCol1Width = 22; // Type/Description column
                    int discountCol2Width = 6;  // Orders column
                    int discountCol3Width = 20; // Amount column
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row - format as 3 columns
                    sb.AppendLine(FormatTableRow("Type", "", "Amount (" + currency + ")", discountCol1Width, discountCol2Width, discountCol3Width));
                    sb.AppendLine(new string('-', 50));

                    // Get discount values and order counts from zReportStats
                    //decimal posDiscounts = zReportStats?.PosDiscount ?? 0.00m;
                    //decimal platformDiscounts = zReportStats?.PlatformDiscount ?? 0.00m;
                    //decimal totalDiscounts = posDiscounts + platformDiscounts;
                    
                    // Discounts subheading
                    sb.AppendLine(FormatTableRow("Discount", "", "", discountCol1Width, discountCol2Width, discountCol3Width));
                    // Table rows - use FormatTableRow for 3 columns
                    sb.AppendLine(FormatTableRow("-POS", "", $"{currency} {zReportStats.PosDiscount:F2}", discountCol1Width, discountCol2Width, discountCol3Width));
                    
                    // Display individual platforms where PlatformId != 9
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var platformName = "-" + kvp.Key;
                                var discountOrderCount = platform.DiscountSummary?.DiscountOrderCount ?? 0;
                                var discount = ParseCurrencyString(platform.DiscountSummary?.Discount);
                                
                                // Handle long platform names with text wrapping
                                var wrappedLines = WrapPlatformName(platformName, discountCol1Width);
                                if (wrappedLines.Count > 0)
                                {
                                    // First line: platform name (first part) + orders + discount amount
                                    sb.AppendLine(FormatTableRow(wrappedLines[0], "", $"{currency} {discount:F2}", discountCol1Width, discountCol2Width, discountCol3Width));
                                    
                                    // Additional lines: wrapped platform name parts only (empty orders and amount columns)
                                    for (int i = 1; i < wrappedLines.Count; i++)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", discountCol1Width, discountCol2Width, discountCol3Width));
                                    }
                                }
                            }
                        }
                    }
                    


                    // Voucher subheading
                    sb.AppendLine(FormatTableRow("Voucher Discount", "", "", discountCol1Width, discountCol2Width, discountCol3Width));
                    
                    // POS Voucher Discount (PlatformId == 9) - always show
                    var posVoucherDiscount = 0m;
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            if (platform.PlatformId == 9)
                            {
                                posVoucherDiscount = ParseCurrencyString(platform.DiscountSummary?.VoucherDiscount);
                                break;
                            }
                        }
                    }
                    sb.AppendLine(FormatTableRow("-POS", "", $"{currency} {posVoucherDiscount:F2}", discountCol1Width, discountCol2Width, discountCol3Width));
                    
                    // Display individual platforms' voucher discounts where PlatformId != 9
                    if (zReportStats.PlatformStats != null)
                    {
                        foreach (var kvp in zReportStats.PlatformStats)
                        {
                            var platform = kvp.Value;
                            // Only include platforms where PlatformId != 9
                            if (platform.PlatformId != 9)
                            {
                                var platformName = "-" + kvp.Key;
                                var voucherDiscount = ParseCurrencyString(platform.DiscountSummary?.VoucherDiscount);
                                
                                // Handle long platform names with text wrapping
                                var wrappedLines = WrapPlatformName(platformName, discountCol1Width);
                                if (wrappedLines.Count > 0)
                                {
                                    // First line: platform name (first part) + empty orders + voucher discount amount
                                    sb.AppendLine(FormatTableRow(wrappedLines[0], "", $"{currency} {voucherDiscount:F2}", discountCol1Width, discountCol2Width, discountCol3Width));
                                    
                                    // Additional lines: wrapped platform name parts only (empty orders and amount columns)
                                    for (int i = 1; i < wrappedLines.Count; i++)
                                    {
                                        sb.AppendLine(FormatTableRow(wrappedLines[i], "", "", discountCol1Width, discountCol2Width, discountCol3Width));
                                    }
                                }
                            }
                        }
                    }

                     // Total Discounts
                    sb.AppendLine(FormatTableRow("Total Discounts", "", $"{currency} {zReportStats.TotalDiscountCalculated:F2}", discountCol1Width, discountCol2Width, discountCol3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));
                    sb.AppendLine(); // Add empty line for spacing

                    // Cash Drawer Summary Section
                    sb.AppendLine("**Cash Drawer Summary**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Check if we have cash drawer session from the API (only ONE session for cash drawer summary)
                    if (cashSessions == null || cashSessions.Count == 0)
                    {
                        // No sessions found
                        sb.AppendLine("No cash session in this date range");
                        sb.AppendLine(new string('=', 50));
                    }
                    else
                    {
                        // Define column widths for cash drawer summary table (3 columns, using FormatTableRow for consistency)
                        // Use empty middle column to maintain 3-column structure like other sections
                        int cashDrawerCol1Width = 28; // Description column (match Tender Summary col1 width)
                        int cashDrawerCol2Width = 8;  // Empty middle column (for consistency)
                        int cashDrawerCol3Width = 12; // Amount column (match Tender Summary col3 width)
                        
                        // Only print ONE session (the first/active session)
                        var cashSession = cashSessions.FirstOrDefault();
                        if (cashSession != null)
                        {
                            // Flag to indicate table rows should use font size 11
                            sb.AppendLine(":TABLE11:");
                            
                            // Header row - use FormatTableRow for consistent alignment
                            sb.AppendLine(FormatTableRow("Description", "", "Amount (" + currency + ")", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(new string('-', 50));
                            
                            // Cash drawer summary values from session - use FormatTableRow with empty middle column
                            sb.AppendLine(FormatTableRow("Starting cash", "", $"{currency} {cashSession.OpeningBalance:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("POS Sales", "", $"{currency} {cashSession.TotalSalesAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("Other Sales", "", $"{currency} {cashSession.OtherSalesAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("Cash Refunds", "", $"{currency} {cashSession.TotalRefundAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow(" - Cash Sale Cash refund", "", $"{currency} {(cashSession.TotalCashSaleCashRefundAmount + cashSession.TotalOtherCashSaleCashRefundAmount):F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow(" - Card Sale Cash refund", "", $"{currency} {cashSession.TotalCardSaleCashRefundAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("Cash in", "", $"{currency} {cashSession.TotalInAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("Cash out", "", $"{currency} {cashSession.TotalOutAmount:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            sb.AppendLine(FormatTableRow("Expected Cash Balance", "", $"{currency} {cashSession.ClosingBalanceExpected:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            
                            // Actual Counted Cash - Only show for Z reports (when session is closed)
                            if (session.ClosedAt.HasValue)
                            {
                                sb.AppendLine(FormatTableRow("Actual Counted Cash", "", $"{currency} {cashSession.ClosingBalanceCounted:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            }
                            
                            // Difference (Over/Short)
                            sb.AppendLine(FormatTableRow("Difference (Over/Short)", "", $"{currency} {cashSession.Difference:F2}", cashDrawerCol1Width, cashDrawerCol2Width, cashDrawerCol3Width));
                            
                            // End table flag
                            sb.AppendLine(":ENDTABLE11:");
                        }
                        
                        sb.AppendLine(new string('=', 50));
                    }

                   // Tax Summary Section
                    sb.AppendLine("**Tax Summary**");
                    
                    // Define column widths for tax summary table (3 columns)
                    int taxCol1Width = 18; // Tax Rate column
                    int taxCol2Width = 18; // Tax Amount column
                    int taxCol3Width = 14; // Order Amount column
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row - format as 3 columns using FormatTableRow for consistency
                    sb.AppendLine(FormatTableRow("Tax Rate", "Tax(" + currency + ")", "Order(" + currency + ")", taxCol1Width, taxCol2Width, taxCol3Width));
                    sb.AppendLine(new string('-', 50));

                    // Tax summary values - get from zReportStats.TaxSummary (TaxSummaryModel from GetZReportStatsAsync)
                    try
                    {
                        if (zReportStats != null && zReportStats.TaxSummary != null && zReportStats.TaxSummary.TaxBreakdown != null && zReportStats.TaxSummary.TaxBreakdown.Count > 0)
                        {
                            decimal totalTaxAmount = 0m;
                            decimal totalOrderAmount = 0m;
                            
                            // Iterate through tax breakdown items
                            foreach (var taxItem in zReportStats.TaxSummary.TaxBreakdown.Values)
                            {
                                if (taxItem == null) continue;
                                
                                // Use TaxRateDisplay property which formats the rate (e.g., "20.00%")
                                // Handle null TaxRate safely
                                string taxRateDisplay = "N/A";
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(taxItem.TaxRate))
                                    {
                                        taxRateDisplay = taxItem.TaxRateDisplay;
                                    }
                                    else
                                    {
                                        taxRateDisplay = "N/A";
                                    }
                                }
                                catch
                                {
                                    taxRateDisplay = taxItem.TaxRate ?? "N/A";
                                }
                                
                                // Format tax rate with tax code: "taxrate% + tax_code"
                                string taxRateWithCode = $"{taxRateDisplay} + {taxItem.TaxCode ?? "N/A"}";
                                
                                decimal taxAmount = taxItem.TaxAmount;
                                decimal orderAmount = taxItem.OrderAmount;
                                
                                // Format row using FormatTableRow for consistent alignment
                                sb.AppendLine(FormatTableRow(taxRateWithCode, $"{currency} {taxAmount:F2}", $"{currency} {orderAmount:F2}", taxCol1Width, taxCol2Width, taxCol3Width));
                                
                                totalTaxAmount += taxAmount;
                                totalOrderAmount += orderAmount;
                            }
                            
                            // Totals row (only if we have items)
                            if (totalTaxAmount > 0m || totalOrderAmount > 0m)
                            {
                                sb.AppendLine(FormatTableRow("Total Taxes", $"{currency} {totalTaxAmount:F2}", $"{currency} {totalOrderAmount:F2}", taxCol1Width, taxCol2Width, taxCol3Width));
                            }
                        }
                        else
                        {
                            // No tax data available
                            sb.AppendLine(FormatTableRow("No Tax Data", "", "", taxCol1Width, taxCol2Width, taxCol3Width));
                        }
                    }
                    catch (Exception taxEx)
                    {
                        // If there's an error processing tax data, show error message
                        System.Diagnostics.Debug.WriteLine($"[GenerateReportReceiptContent] Error processing tax data: {taxEx.Message}");
                        sb.AppendLine(FormatTableRow("Error loading tax data", "", "", taxCol1Width, taxCol2Width, taxCol3Width));
                    }
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));

                    // Net Sales Summary Section
                    sb.AppendLine("**Net Sales Summary**");
                    sb.AppendLine(new string('-', 50));
                    
                    // Define column widths for net sales summary table (3 columns, using FormatTableRow for consistency)
                    // Use empty middle column to maintain 3-column structure like other sections
                    int netSalesCol1Width = 28; // Calculation column (match Tender Summary col1 width)
                    int netSalesCol2Width = 8;  // Empty middle column (for consistency)
                    int netSalesCol3Width = 12; // Result column (match Tender Summary col3 width)
                    
                    // Flag to indicate table rows should use font size 11
                    sb.AppendLine(":TABLE11:");
                    
                    // Header row - use FormatTableRow for consistent alignment
                    sb.AppendLine(FormatTableRow("Calculation", "", "Result (" + currency + ")", netSalesCol1Width, netSalesCol2Width, netSalesCol3Width));
                    sb.AppendLine(new string('-', 50));
                    
                    // Net sales summary values from zReportStats (these are already formatted currency strings)
                    // Format them properly for display
                    string FormatNetSalesValue(string value)
                    {
                        // If value is already a formatted currency string, use it as-is
                        // Otherwise, format it as currency
                        if (string.IsNullOrWhiteSpace(value))
                            return $"{currency} 0.00";
                        
                        // Check if it already contains currency symbol
                        if (value.Contains(currency) || value.Contains("£") || value.Contains("$") || value.Contains("Rs"))
                            return value;
                        
                        // Try to parse and format
                        var cleaned = value.Replace(",", "").Replace(" ", "").Trim();
                        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result))
                        {
                            return $"{currency} {result:F2}";
                        }
                        return value;
                    }
                    
                    // Table rows - use FormatTableRow with empty middle column
                    sb.AppendLine(FormatTableRow("Gross Sales", "", FormatNetSalesValue(zReportStats.TotalGrossSales), netSalesCol1Width, netSalesCol2Width, netSalesCol3Width));
                    sb.AppendLine(FormatTableRow("Discounts", "", FormatNetSalesValue(zReportStats.TotalDiscount), netSalesCol1Width, netSalesCol2Width, netSalesCol3Width));
                    sb.AppendLine(FormatTableRow("Refunds (Cash + Card)", "", FormatNetSalesValue(zReportStats.TotalRefunds), netSalesCol1Width, netSalesCol2Width, netSalesCol3Width));
                    sb.AppendLine(FormatTableRow("Net Sales", "", FormatNetSalesValue(zReportStats.TotalNetSales), netSalesCol1Width, netSalesCol2Width, netSalesCol3Width));
                    
                    // End table flag
                    sb.AppendLine(":ENDTABLE11:");
                    sb.AppendLine(new string('=', 50));
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging
                System.Diagnostics.Debug.WriteLine($"[GenerateReportReceiptContent] Exception occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GenerateReportReceiptContent] Stack trace: {ex.StackTrace}");
                
                // If there's an error, still generate basic content
                sb.AppendLine("**REPORT**");
                sb.AppendLine(new string('=', 50));
                sb.AppendLine($"Session ID: {session.Id}");
                sb.AppendLine($"Report Date & Time: {FormatDateWithTime(DateTime.Now)}");
                sb.AppendLine(new string('=', 50));
            }

            return sb.ToString();
        }

        public async Task PrintReportReceiptAsync(CashDrawerSessionModel session, POS_UI.Models.ZReportStatsModel zReportStats = null, List<POS_UI.Models.CashDrawerSessionModel> cashSessions = null, POS_UI.Models.TaxSummaryModel taxData = null)
        {
            try
            {
                if (session == null) return;

                var printersService = PrintersService.Instance;
                var receiptContent = GenerateReportReceiptContent(session, zReportStats, cashSessions, taxData);

                // OPTIMIZATION: Run printer operations in PARALLEL to reduce total print time
                var printTasks = new List<Task>();
                
                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        if (!printer.IsActive) continue;
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null || !printerSettings.MainReceipt) continue;
                        int copiesToPrint = Math.Max(1, printerSettings.MainReceiptCount);
                        
                        // Create print task for this printer
                        var printTask = Task.Run(async () =>
                        {
                            try
                            {
                                for (int i = 0; i < copiesToPrint; i++)
                                {
                                    await PrintToPrinterAsync(printer.DeviceName, receiptContent);
                                }
                            }
                            catch (Exception printerEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[PrintReportReceiptAsync] Failed to print to {printer.DeviceName}: {printerEx.Message}");
                            }
                        });
                        
                        printTasks.Add(printTask);
                    }
                    catch (Exception printerEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PrintReportReceiptAsync] Failed to setup print for {printer.DeviceName}: {printerEx.Message}");
                    }
                }
                
                // Wait for all print operations to complete
                if (printTasks.Count > 0)
                {
                    await Task.WhenAll(printTasks);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintReportReceiptAsync] Exception occurred: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[PrintReportReceiptAsync] Stack trace: {ex.StackTrace}");
                // Don't re-throw - allow silent failure for printing
            }
        }

        private OrderModel MapJsonToOrderModel(JsonElement order)
        {
            var model = new OrderModel();
            // Orders parsed here are from Laravel/PHP incoming orders
            model.IsFromPhpApi = true;

            int ParseInt(JsonElement element)
            {
                try
                {
                    if (element.ValueKind == JsonValueKind.Number)
                    {
                        return element.GetInt32();
                    }
                    if (element.ValueKind == JsonValueKind.String)
                    {
                        var raw = element.GetString();
                        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
                catch { }
                return 0;
            }

            // Identifiers
            if (order.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number)
            {
                model.ApiId = idProp.GetInt32();
            }
            if (order.TryGetProperty("order_number", out var onProp) && onProp.ValueKind == JsonValueKind.String)
            {
                model.OrderNumber = onProp.GetString();
            }
            if (order.TryGetProperty("display_order_id", out var doiProp) && doiProp.ValueKind == JsonValueKind.String)
            {
                model.DisplayOrderId = doiProp.GetString();
            }

            // Platform / source - prefer first non-empty value
            string platformCandidate = null;
            if (order.TryGetProperty("delivery_platform_name", out var dpnProp) && dpnProp.ValueKind == JsonValueKind.String)
            {
                var val = dpnProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) platformCandidate = val;
            }
            if (string.IsNullOrWhiteSpace(platformCandidate) && order.TryGetProperty("platform", out var platProp) && platProp.ValueKind == JsonValueKind.String)
            {
                var val = platProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) platformCandidate = val;
            }
            if (string.IsNullOrWhiteSpace(platformCandidate) && order.TryGetProperty("source", out var sourceProp) && sourceProp.ValueKind == JsonValueKind.String)
            {
                var val = sourceProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) platformCandidate = val;
            }
            if (string.IsNullOrWhiteSpace(platformCandidate) && order.TryGetProperty("platform_name", out var platformNameProp) && platformNameProp.ValueKind == JsonValueKind.String)
            {
                var val = platformNameProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) platformCandidate = val;
            }
            if (string.IsNullOrWhiteSpace(platformCandidate) && order.TryGetProperty("order_source", out var orderSourceProp) && orderSourceProp.ValueKind == JsonValueKind.String)
            {
                var val = orderSourceProp.GetString();
                if (!string.IsNullOrWhiteSpace(val)) platformCandidate = val;
            }
            model.PlatformName = platformCandidate;

            model.DeliveryPlatfornName = platformCandidate; // Also set DeliveryPlatfornName for receipt printing

            if (!string.IsNullOrWhiteSpace(platformCandidate))
            {
                model.Platform = platformCandidate;
            }

            if (order.TryGetProperty("platform_id", out var platformIdProp))
            {
                var parsed = ParseInt(platformIdProp);
                if (parsed > 0) model.PlatformId = parsed;
            }
            if (model.PlatformId == 0 && order.TryGetProperty("delivery_platform_id", out var deliveryPlatformIdProp))
            {
                var parsed = ParseInt(deliveryPlatformIdProp);
                if (parsed > 0) model.PlatformId = parsed;
            }
            if (model.PlatformId == 0 && order.TryGetProperty("platformId", out var platformIdCamelProp))
            {
                var parsed = ParseInt(platformIdCamelProp);
                if (parsed > 0) model.PlatformId = parsed;
            }
            // Incoming orders: set PlatformId2 from same source so receipt/kitchen logic can detect table orders (e.g. platform 8)
            if (model.PlatformId > 0)
            {
                model.PlatformId2 = model.PlatformId;
            }
            // Timestamps
            model.CreatedAt = DateTime.Now;
            if (order.TryGetProperty("created_at", out var createdProp) && createdProp.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(createdProp.GetString(), out var created))
                {
                    model.CreatedAt = created;
                }
            }

            DateTime? ResolveDateTimeFromPossiblyTimeOnly(string raw)
            {
                if (string.IsNullOrWhiteSpace(raw)) return null;
                // If raw contains a date part, parse directly
                bool hasDate = raw.Contains("-") || raw.Contains("/");
                if (hasDate)
                {
                    if (DateTime.TryParse(raw, out var dtWithDate)) return dtWithDate;
                    return null;
                }
                // Time-only: combine with created date and roll to next day when earlier than created time
                if (DateTime.TryParse(raw, out var temp))
                {
                    var created = model.CreatedAt == default ? DateTime.Now : model.CreatedAt;
                    var baseDate = created.Date;
                    var candidate = new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, temp.Hour, temp.Minute, temp.Second);
                    if (candidate < created)
                    {
                        candidate = candidate.AddDays(1);
                    }
                    return candidate;
                }
                return null;
            }

            if (order.TryGetProperty("scheduled_time", out var schedProp) && schedProp.ValueKind == JsonValueKind.String)
            {
                var raw = schedProp.GetString();
                var resolved = ResolveDateTimeFromPossiblyTimeOnly(raw);
                if (resolved.HasValue) model.ScheduledTime = resolved.Value;
            }
            else if (order.TryGetProperty("delivery_time", out var delTimeProp) && delTimeProp.ValueKind == JsonValueKind.String)
            {
                var raw = delTimeProp.GetString();
                var resolved = ResolveDateTimeFromPossiblyTimeOnly(raw);
                if (resolved.HasValue)
                {
                    model.ScheduledTime = resolved.Value;
                    model.DeliveryDateTime = resolved.Value;
                }
            }

            // Order type
            var typeText = "Take Away";
            if (order.TryGetProperty("delivery_type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            {
                typeText = typeProp.GetString() ?? typeText;
            }
            switch (typeText.Trim().ToUpperInvariant())
            {
                case "DINEIN":
                case "DINE IN":
                case "DINE_IN":
                    model.OrderType = OrderType.DineIn; break;
                case "DELIVERY":
                    model.OrderType = OrderType.Delivery; break;
                case "COLLECTION":
                case "TAKEAWAY":
                case "TAKE AWAY":
                    model.OrderType = OrderType.TakeAway; break;
                default:
                    model.OrderType = OrderType.TakeAway; break;
            }

            // Table number (for dine-in)
            if (order.TryGetProperty("table_number", out var tableProp) && tableProp.ValueKind == JsonValueKind.String)
            {
                if (int.TryParse(tableProp.GetString(), out var tn)) model.TableNumber = tn;
            }
            else if (order.TryGetProperty("table_id", out var tableIdProp) && tableIdProp.ValueKind == JsonValueKind.Number)
            {
                model.TableNumber = tableIdProp.GetInt32();
            }
            if (order.TryGetProperty("table_name", out var tableNameProp) && tableNameProp.ValueKind == JsonValueKind.String)
            {
                model.TableName = tableNameProp.GetString();
            }
            if (order.TryGetProperty("table_order_method", out var tableOrderMethodProp) && tableOrderMethodProp.ValueKind == JsonValueKind.String)
            {
                model.TableOrderMethod = tableOrderMethodProp.GetString();
            }

            // Customer
            if (order.TryGetProperty("delivergate_customer", out var custProp) && custProp.ValueKind == JsonValueKind.Object)
            {
                var first = custProp.TryGetProperty("first_name", out var fn) && fn.ValueKind == JsonValueKind.String ? fn.GetString() : "";
                var last = custProp.TryGetProperty("last_name", out var ln) && ln.ValueKind == JsonValueKind.String ? ln.GetString() : "";
                model.CustomerName = ($"{first} {last}").Trim();
                if (custProp.TryGetProperty("phone", out var ph) && ph.ValueKind == JsonValueKind.String)
                {
                    model.CustomerPhone = ph.GetString();
                }
            }

            // Address (for delivery)
            if (order.TryGetProperty("delivery_address", out var addrProp))
            {
                if (addrProp.ValueKind == JsonValueKind.String)
                {
                    model.DeliveryAddress = addrProp.GetString();
                }
                else if (addrProp.ValueKind == JsonValueKind.Object)
                {
                    // Try typical fields
                    var parts = new List<string>();
                    if (addrProp.TryGetProperty("line1", out var l1) && l1.ValueKind == JsonValueKind.String) parts.Add(l1.GetString());
                    if (addrProp.TryGetProperty("line2", out var l2) && l2.ValueKind == JsonValueKind.String) parts.Add(l2.GetString());
                    if (addrProp.TryGetProperty("city", out var city) && city.ValueKind == JsonValueKind.String) parts.Add(city.GetString());
                    if (addrProp.TryGetProperty("postcode", out var pc) && pc.ValueKind == JsonValueKind.String) parts.Add(pc.GetString());
                    model.DeliveryAddress = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                }
            }

            // Fallbacks for delivery address (incoming orders may use different structures)
            if (string.IsNullOrWhiteSpace(model.DeliveryAddress))
            {
                // shipping_details object with address_line_1/2, city, postcode
                if (order.TryGetProperty("shipping_details", out var ship) && ship.ValueKind == JsonValueKind.Object)
                {
                    var parts = new List<string>();
                    if (ship.TryGetProperty("flat_no", out var flatNo) && flatNo.ValueKind != JsonValueKind.Null && flatNo.ValueKind == JsonValueKind.String)
                        parts.Add(flatNo.GetString());
                    if (ship.TryGetProperty("address_line_1", out var s1) && s1.ValueKind != JsonValueKind.Null && s1.ValueKind == JsonValueKind.String)
                        parts.Add(s1.GetString());
                    if (ship.TryGetProperty("address_line_2", out var s2) && s2.ValueKind != JsonValueKind.Null && s2.ValueKind == JsonValueKind.String)
                        parts.Add(s2.GetString());
                    if (ship.TryGetProperty("city", out var scity) && scity.ValueKind != JsonValueKind.Null && scity.ValueKind == JsonValueKind.String)
                        parts.Add(scity.GetString());
                    if (ship.TryGetProperty("postcode", out var spc) && spc.ValueKind != JsonValueKind.Null && spc.ValueKind == JsonValueKind.String)
                        parts.Add(spc.GetString());

                    var composed = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                    if (!string.IsNullOrWhiteSpace(composed))
                    {
                        model.DeliveryAddress = composed;
                    }
                }

                // delivergate_customer.address consolidated string
                if (string.IsNullOrWhiteSpace(model.DeliveryAddress)
                    && order.TryGetProperty("delivergate_customer", out var dgc) && dgc.ValueKind == JsonValueKind.Object)
                {
                    if (dgc.TryGetProperty("address", out var fullAddr) && fullAddr.ValueKind == JsonValueKind.String)
                    {
                        var address = fullAddr.GetString();
                        if (!string.IsNullOrWhiteSpace(address))
                        {
                            model.DeliveryAddress = address;
                        }
                    }
                }
            }

            // Notes
            if (order.TryGetProperty("note", out var noteProp) && noteProp.ValueKind == JsonValueKind.String)
            {
                model.OrderNotes = noteProp.GetString();
            }

            // Totals
            if (order.TryGetProperty("sub_total", out var subProp))
            {
                model.ApiSubTotal = TryParseDecimal(subProp);
            }
            if (order.TryGetProperty("total_amount", out var totProp))
            {
                model.ApiTotal = TryParseDecimal(totProp);
            }
            if (order.TryGetProperty("discount_amount", out var discProp))
            {
                model.DiscountAmount = TryParseDecimal(discProp);
            }
            if ((model.DiscountAmount <= 0m) && order.TryGetProperty("discount", out var discountProp))
            {
                model.DiscountAmount = TryParseDecimal(discountProp);
            }
            if (order.TryGetProperty("coupon_amount", out var coupProp))
            {
                model.CouponAmount = TryParseDecimal(coupProp);
            }
            if (order.TryGetProperty("delivery_charge", out var delChargeProp))
            {
                model.DeliveryCharge = TryParseDecimal(delChargeProp);
            }
            // Fallbacks for delivery charges in incoming orders
            if (model.DeliveryCharge <= 0m)
            {
                if (order.TryGetProperty("shipping_total", out var shipTotalProp))
                {
                    model.DeliveryCharge = TryParseDecimal(shipTotalProp);
                }
                else if (order.TryGetProperty("shipping_total_amount", out var shipTotalAmtProp))
                {
                    model.DeliveryCharge = TryParseDecimal(shipTotalAmtProp);
                }
            }

            // Additional components: BOGO and Total Fee (shop fees)
            if (order.TryGetProperty("bogo_discount", out var bogoProp))
            {
                model.BogoDiscount = TryParseDecimal(bogoProp);
            }
            // Loyalty / Reward discount (divide by 100 if sent in cents)
            if (order.TryGetProperty("loyalty", out var loyaltyObj) && loyaltyObj.ValueKind == JsonValueKind.Object)
            {
                if (loyaltyObj.TryGetProperty("redeemed_amount", out var redeemed))
                {
                    try
                    {
                        if (redeemed.ValueKind == JsonValueKind.String)
                        {
                            if (decimal.TryParse(redeemed.GetString()?.Replace(" ", string.Empty), out var raw))
                            {
                                model.RewardDiscount = raw / 100m;
                            }
                        }
                        else if (redeemed.ValueKind == JsonValueKind.Number)
                        {
                            model.RewardDiscount = redeemed.GetDecimal() / 100m;
                        }
                    }
                    catch { }
                }
            }
            // Collect individual shop fees for printing labels
            var orderShopFees = new List<OrderShopFeeModel>();
            // New format: shop_fees (amount in cents)
            if (order.TryGetProperty("shop_fees", out var shopFeesArr) && shopFeesArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var fee in shopFeesArr.EnumerateArray())
                {
                    try
                    {
                        decimal amount = 0m;
                        if (fee.TryGetProperty("amount", out var amountEl))
                        {
                            amount = DivideCents(TryParseDecimal(amountEl));
                        }
                        if (amount <= 0m) continue;

                        var name = fee.TryGetProperty("fee_name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                            ? (nameEl.GetString() ?? "Shop Fee")
                            : "Shop Fee";
                        var type = fee.TryGetProperty("fee_type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String
                            ? typeEl.GetString()
                            : null;
                        decimal feeValue = 0m;
                        if (fee.TryGetProperty("fee", out var feeValEl))
                        {
                            feeValue = TryParseDecimal(feeValEl);
                        }
                        var isMandatory = fee.TryGetProperty("mandatory", out var mandatoryEl) && mandatoryEl.ValueKind == JsonValueKind.True;
                        orderShopFees.Add(new OrderShopFeeModel { Name = name, Amount = amount, FeeType = type, FeeValue = feeValue, IsMandatory = isMandatory });
                    }
                    catch { }
                }
            }
            // Legacy format: shopFee (amount already in major units)
            if (order.TryGetProperty("shopFee", out var shopFeeArr) && shopFeeArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var fee in shopFeeArr.EnumerateArray())
                {
                    try
                    {
                        decimal legacyAmount = 0m;
                        if (fee.TryGetProperty("amount", out var amountEl2))
                        {
                            legacyAmount = TryParseDecimal(amountEl2);
                        }
                        if (legacyAmount <= 0m) continue;
                        var name = fee.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                            ? (nEl.GetString() ?? "Shop Fee")
                            : "Shop Fee";
                        var isMandatory = fee.TryGetProperty("mandatory", out var mandatoryEl2) && mandatoryEl2.ValueKind == JsonValueKind.True;
                        orderShopFees.Add(new OrderShopFeeModel { Name = name, Amount = legacyAmount, FeeType = null, FeeValue = 0m, IsMandatory = isMandatory });
                    }
                    catch { }
                }
            }
            if (orderShopFees.Count > 0)
            {
                model.OrderShopFees = orderShopFees;
                if ((model.TotalFee <= 0m))
                {
                    model.TotalFee = orderShopFees.Sum(f => f.Amount);
                }
            }

            // Payment
            if (order.TryGetProperty("payment_status", out var payStat) && payStat.ValueKind == JsonValueKind.String)
            {
                var ps = payStat.GetString();
                model.PaymentStatus = ps;
                model.IsPaid = string.Equals(ps, "PAID", StringComparison.OrdinalIgnoreCase);
            }
            if (order.TryGetProperty("payment_method", out var payMeth) && payMeth.ValueKind == JsonValueKind.String)
            {
                model.PaymentMethod = payMeth.GetString();
            }
            if (order.TryGetProperty("payment_mode", out var payMode) && payMode.ValueKind == JsonValueKind.String)
            {
                model.PaymentMode = payMode.GetString();
            }

            // Items
            var items = new List<OrderItem>();
            if (order.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in itemsProp.EnumerateArray())
                {
                    var orderItem = new OrderItem();
                    if (item.TryGetProperty("item_name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                    {
                        orderItem.Name = nameProp.GetString();
                    }
                    int ResolveQuantity(JsonElement element)
                    {
                        try
                        {
                            if (element.ValueKind == JsonValueKind.Number)
                            {
                                return element.GetInt32();
                            }
                            if (element.ValueKind == JsonValueKind.String)
                            {
                                var raw = element.GetString();
                                if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out var parsed))
                                {
                                    return parsed;
                                }
                            }
                        }
                        catch { }
                        return 0;
                    }

                    int quantity = 0;
                    if (item.TryGetProperty("quantity", out var qtyProp))
                    {
                        quantity = ResolveQuantity(qtyProp);
                    }
                    if (quantity <= 0 && item.TryGetProperty("qty", out var qtyAltProp))
                    {
                        quantity = ResolveQuantity(qtyAltProp);
                    }
                    if (quantity <= 0 && item.TryGetProperty("Quantity", out var qtyPascalProp))
                    {
                        quantity = ResolveQuantity(qtyPascalProp);
                    }
                    if (quantity <= 0 && item.TryGetProperty("item_qty", out var qtySnakeProp))
                    {
                        quantity = ResolveQuantity(qtySnakeProp);
                    }
                    if (quantity <= 0)
                    {
                        quantity = 1;
                    }
                    orderItem.Quantity = quantity;

                    decimal unitPrice = 0m;
                    if (item.TryGetProperty("display_price", out var priceProp))
                    {
                        unitPrice = TryParseDecimal(priceProp);
                    }
                    orderItem.Price = unitPrice;
                    // Prefer API-provided line total (already includes modifiers) for printing
                    decimal apiLineTotal = 0m;
                    if (item.TryGetProperty("total", out var totalProp))
                    {
                        apiLineTotal = DivideCents(TryParseDecimal(totalProp));
                    }
                    else
                    {
                        apiLineTotal = unitPrice * Math.Max(1, orderItem.Quantity);
                    }
                    orderItem.ApiItemPrice = apiLineTotal;

                    if (item.TryGetProperty("note", out var itemNote) && itemNote.ValueKind == JsonValueKind.String)
                    {
                        orderItem.Note = itemNote.GetString();
                    }

                    // Parse item-level discount (incoming API provides cents)
                    if (item.TryGetProperty("discount_amount", out var itemDisc))
                    {
                        decimal discVal = 0m;
                        if (itemDisc.ValueKind == JsonValueKind.Number)
                        {
                            discVal = Math.Round(itemDisc.GetDecimal() / 100m, 2, MidpointRounding.AwayFromZero);
                        }
                        else if (itemDisc.ValueKind == JsonValueKind.String && decimal.TryParse(itemDisc.GetString(), out var ds))
                        {
                            discVal = Math.Round(ds / 100m, 2, MidpointRounding.AwayFromZero);
                        }
                        orderItem.ApiDiscountAmount = discVal;
                        orderItem.VisibleDiscountAmount = discVal;
                        orderItem.DisAmount = discVal;
                        if (orderItem.Quantity > 0 && discVal > 0m)
                        {
                            orderItem.UnitDiscountAmount = Math.Round(discVal / orderItem.Quantity, 2, MidpointRounding.AwayFromZero);
                        }
                    }

                    // Parse modifiers → ExternalModifierDetailsForDisplay
                    // Expected shape: modifiers: [ { title, selected_item: [ { title, price_per_item/display_price, modifiers: [ ... ] } ] } ]
                    if (item.TryGetProperty("modifiers", out var modifiersEl) && modifiersEl.ValueKind == JsonValueKind.Array)
                    {
                        var flatDetails = new List<ModifierDetailModel>();
                        foreach (var mod in modifiersEl.EnumerateArray())
                        {
                            ParseModifierGroupForOrderItem(mod, flatDetails, "");
                        }
                        orderItem.ExternalModifierDetailsForDisplay = flatDetails;
                    }

                    // Parse printer_groups and create Product with PrinterGroups
                    if (item.TryGetProperty("printer_groups", out var printerGroupsEl) && printerGroupsEl.ValueKind == JsonValueKind.Array)
                    {
                        var printerGroups = new List<PrinterGroupModel>();
                        foreach (var printerGroupElement in printerGroupsEl.EnumerateArray())
                        {
                            try
                            {
                                var printerGroup = new PrinterGroupModel
                                {
                                    Id = printerGroupElement.TryGetProperty("id", out var pgIdElement) && pgIdElement.ValueKind == JsonValueKind.Number 
                                        ? pgIdElement.GetInt32() 
                                        : 0,
                                    Name = printerGroupElement.TryGetProperty("name", out var pgNameElement) 
                                        ? pgNameElement.GetString() 
                                        : null,
                                    Description = printerGroupElement.TryGetProperty("description", out var pgDescElement) 
                                        ? pgDescElement.GetString() 
                                        : null,
                                    Status = printerGroupElement.TryGetProperty("status", out var pgStatusElement) && 
                                            (pgStatusElement.ValueKind == JsonValueKind.True || 
                                             (pgStatusElement.ValueKind == JsonValueKind.Number && pgStatusElement.GetInt32() == 1) ||
                                             (pgStatusElement.ValueKind == JsonValueKind.String && string.Equals(pgStatusElement.GetString(), "1", StringComparison.OrdinalIgnoreCase))),
                                    CreatedAt = printerGroupElement.TryGetProperty("created_at", out var pgCreatedProp) && pgCreatedProp.ValueKind == JsonValueKind.String 
                                        ? DateTime.Parse(pgCreatedProp.GetString()) 
                                        : DateTime.MinValue,
                                    UpdatedAt = printerGroupElement.TryGetProperty("updated_at", out var pgUpdatedProp) && pgUpdatedProp.ValueKind == JsonValueKind.String 
                                        ? DateTime.Parse(pgUpdatedProp.GetString()) 
                                        : DateTime.MinValue
                                };
                                printerGroups.Add(printerGroup);
                            }
                            catch
                            {
                                // Ignore malformed printer group entries
                            }
                        }

                        // Create or update Product with PrinterGroups
                        if (orderItem.Product == null)
                        {
                            orderItem.Product = new ProductItemModel
                            {
                                ItemName = orderItem.Name
                            };
                        }
                        orderItem.Product.PrinterGroups = printerGroups;
                    }

                    items.Add(orderItem);
                }
            }
            model.Items = items;

            // DO NOT aggregate item-level discounts into order-level DiscountAmount
            // Item-level discounts are displayed at the item level in the receipt
            // Order-level DiscountAmount should only come from API's order-level discount fields
            // (e.g., "discount" or "discount_amount" from the order JSON)
            // This prevents item discounts from incorrectly appearing as order-level discounts

            return model;
        }

        private void ParseModifierGroupForOrderItem(JsonElement group, List<ModifierDetailModel> output, string indent)
        {
            try
            {
                var groupTitle = group.TryGetProperty("title", out var t) ? t.GetString() : "";
                if (group.TryGetProperty("selected_item", out var selectedEl) && selectedEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var sel in selectedEl.EnumerateArray())
                    {
                        var itemTitle = sel.TryGetProperty("title", out var it) ? it.GetString() : "";
                        decimal price = 0m;
                        if (sel.TryGetProperty("price_per_item", out var ppi))
                        {
                            price = DivideCents(TryParseDecimal(ppi));
                        }
                        else if (sel.TryGetProperty("display_price", out var dp))
                        {
                            price = DivideCents(TryParseDecimal(dp));
                        }

                        var name = string.IsNullOrWhiteSpace(groupTitle) ? itemTitle : $"{groupTitle}: {itemTitle}";
                        var isNested = !string.IsNullOrEmpty(indent);
                        output.Add(new ModifierDetailModel(name, price, isNested, indent));

                        // Recurse into nested modifiers
                        if (sel.TryGetProperty("modifiers", out var nested) && nested.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var nestedGroup in nested.EnumerateArray())
                            {
                                ParseModifierGroupForOrderItem(nestedGroup, output, indent + "    ");
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private decimal GetOrderItemDisplayDiscount(OrderItem item, CartService cartService = null, OrderModel order = null)
        {
            if (item == null) return 0m;

            decimal baseDiscount = item.VisibleDiscountAmount;
            if (baseDiscount <= 0m)
            {
                baseDiscount = item.ApiDiscountAmount;
            }
            if (baseDiscount <= 0m)
            {
                baseDiscount = item.DisAmount;
            }

            if (baseDiscount <= 0m)
            {
                return 0m;
            }

            var quantity = item.Quantity > 0 ? item.Quantity : 1;

            // Always scale by quantity for item-level discounts (per-unit discount × quantity = line discount)
            // Default to true for all orders, including POS orders
            bool shouldScaleByQuantity = true;

            // The following conditions are kept for legacy compatibility but with default true,
            // all orders (POS and external) will now multiply discount by quantity
            if (order != null)
            {
                // For POS orders: PlatformId is typically null/0, so default true applies
                // For external orders: explicitly set true based on platform
                if (order.PlatformId == 6 || order.PlatformId2 == 6)
                {
                    shouldScaleByQuantity = true;
                }
                else
                {
                    var platformName = order.PlatformName;
                    if (string.IsNullOrWhiteSpace(platformName))
                    {
                        platformName = order.Platform ?? order.DeliveryPlatfornName;
                    }
                    if (!string.IsNullOrWhiteSpace(platformName))
                    {
                        var normalized = new string(platformName.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
                        if (normalized.Contains("WEBSHOP"))
                        {
                            shouldScaleByQuantity = true;
                        }
                    }
                }

                if (!shouldScaleByQuantity && order.IsFromPhpApi)
                {
                    shouldScaleByQuantity = true;
                }
            }

            if (shouldScaleByQuantity && quantity > 1)
            {
                var scaledDiscount = Math.Round(baseDiscount * quantity, 2, MidpointRounding.AwayFromZero);

                decimal maxReasonableDiscount = 0m;
                if (item.ApiItemPrice > 0m)
                {
                    maxReasonableDiscount = Math.Round(item.ApiItemPrice, 2, MidpointRounding.AwayFromZero);
                }
                else if (item.Price > 0m)
                {
                    maxReasonableDiscount = Math.Round(item.Price * quantity, 2, MidpointRounding.AwayFromZero);
                }

                if (maxReasonableDiscount > 0m && scaledDiscount > maxReasonableDiscount)
                {
                    // If scaling would exceed the line total, assume the discount was already line-level
                    return Math.Round(baseDiscount, 2, MidpointRounding.AwayFromZero);
                }

                return scaledDiscount;
            }

            return Math.Round(baseDiscount, 2, MidpointRounding.AwayFromZero);
        }

        private decimal TryParseDecimal(JsonElement prop)
        {
            try
            {
                switch (prop.ValueKind)
                {
                    case JsonValueKind.Number:
                        return prop.TryGetDecimal(out var d) ? d : 0m;
                    case JsonValueKind.String:
                        var raw = prop.GetString();
                        if (string.IsNullOrWhiteSpace(raw)) return 0m;

                        // Fast path
                        if (decimal.TryParse(raw, out var fast)) return fast;

                        // Normalize: remove currency symbols and whitespace (including non-breaking),
                        // then handle thousands/decimal separators across locales and spaced numbers like "2 300".
                        var cleaned = raw
                            ?.Replace("£", string.Empty)
                            .Replace("$", string.Empty)
                            .Replace("Rs", string.Empty)
                            .Replace("₹", string.Empty)
                            .Replace("€", string.Empty);

                        if (string.IsNullOrEmpty(cleaned)) return 0m;

                        // Remove all Unicode whitespace characters
                        cleaned = new string(cleaned.Where(c => !char.IsWhiteSpace(c)).ToArray());

                        // If the string is now just signs/separators, bail out
                        if (string.IsNullOrEmpty(cleaned)) return 0m;

                        // Heuristics for separators
                        bool hasComma = cleaned.Contains(',');
                        bool hasDot = cleaned.Contains('.');

                        if (hasComma && hasDot)
                        {
                            // If comma appears after dot, treat comma as decimal (e.g., 1.234,56)
                            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
                            {
                                cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
                            }
                            else
                            {
                                // Dot as decimal, commas as thousands (e.g., 1,234.56)
                                cleaned = cleaned.Replace(",", string.Empty);
                            }
                        }
                        else if (hasComma && !hasDot)
                        {
                            // Decide if comma is decimal or thousands based on trailing digits count
                            int lastComma = cleaned.LastIndexOf(',');
                            int digitsAfter = cleaned.Length - lastComma - 1;
                            if (digitsAfter == 2)
                            {
                                // Likely decimal comma
                                cleaned = cleaned.Replace(',', '.');
                            }
                            else
                            {
                                // Likely thousands separator(s)
                                cleaned = cleaned.Replace(",", string.Empty);
                            }
                        }
                        // else: only dot or none → already fine

                        // Final parse using invariant culture
                        if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result))
                        {
                            return result;
                        }
                        return 0m;
                    default:
                        return 0m;
                }
            }
            catch { return 0m; }
        }

        private decimal DivideCents(decimal value)
        {
            // Convert minor units (cents) to major currency units
            return Math.Round(value / 100m, 2, MidpointRounding.AwayFromZero);
        }

        private string FormatTableRow(string col1, string col2, string col3, int col1Width, int col2Width, int col3Width)
        {
            // Check if columns have bold markers
            bool col1Bold = col1.Contains("**");
            bool col2Bold = col2.Contains("**");
            bool col3Bold = col3.Contains("**");
            
            // Remove bold markers for formatting
            var col1Text = col1.Replace("**", "");
            var col2Text = col2.Replace("**", "");
            var col3Text = col3.Replace("**", "");
            
            // Format column 1: left-align, truncate if too long
            if (col1Text.Length > col1Width)
            {
                col1Text = col1Text.Substring(0, col1Width - 3) + "...";
            }
            else
            {
                col1Text = col1Text.PadRight(col1Width);
            }
            
            // Format column 2: right-align numbers
            col2Text = col2Text.PadLeft(col2Width);
            
            // Format column 3: right-align numbers
            col3Text = col3Text.PadLeft(col3Width);
            
            // Restore bold markers if they existed
            if (col1Bold) col1Text = $"**{col1Text.Replace("**", "")}**";
            if (col2Bold) col2Text = $"**{col2Text.Replace("**", "")}**";
            if (col3Bold) col3Text = $"**{col3Text.Replace("**", "")}**";
            
            // Return formatted row with pipe separator for easy parsing in printing code
            return $"{col1Text}|{col2Text}|{col3Text}";
        }

        private decimal ParseCurrencyString(string currencyString)
        {
            if (string.IsNullOrWhiteSpace(currencyString))
                return 0m;

            // Remove currency symbols, commas, and spaces, then parse
            var cleaned = currencyString.Replace("£", "").Replace("$", "").Replace("€", "").Replace(",", "").Replace(" ", "").Trim();
            if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var result))
                return result;

            return 0m;
        }

        private List<string> WrapPlatformName(string platformName, int maxWidth)
        {
            var wrappedLines = new List<string>();
            if (string.IsNullOrWhiteSpace(platformName))
            {
                wrappedLines.Add("");
                return wrappedLines;
            }

            // If the name fits in one line, return it as is
            if (platformName.Length <= maxWidth)
            {
                wrappedLines.Add(platformName);
                return wrappedLines;
            }

            // Split by words to wrap at word boundaries
            var words = platformName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var currentLine = "";

            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                
                // If adding this word would exceed the width, start a new line
                if (candidate.Length > maxWidth)
                {
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        wrappedLines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        // Single word is too long, split it at maxWidth
                        var remainingWord = word;
                        while (remainingWord.Length > maxWidth)
                        {
                            wrappedLines.Add(remainingWord.Substring(0, maxWidth));
                            remainingWord = remainingWord.Substring(maxWidth);
                        }
                        currentLine = remainingWord;
                    }
                }
                else
                {
                    currentLine = candidate;
                }
            }

            // Add the last line if it has content
            if (!string.IsNullOrEmpty(currentLine))
            {
                wrappedLines.Add(currentLine);
            }

            return wrappedLines;
        }

        private string FormatDateWithTime(DateTime dateTime)
        {
            var shopDetails = GlobalDataService.Instance.ShopDetails;
            string dateFormat;
            
            if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
            {
                var countryCode = shopDetails.CountryCode.Trim().ToUpper();
                
                // Format date part based on country code (same logic as CountryBasedDateConverter)
                // Include time part (hh:mm tt) in all formats
                switch (countryCode)
                {
                    case "LK":
                        dateFormat = "MM/dd/yyyy hh:mm tt";
                        break;
                    case "GB":
                        dateFormat = "dd/MM/yyyy hh:mm tt";
                        break;
                    default:
                        dateFormat = "yyyy-MM-dd hh:mm tt";
                        break;
                }
            }
            else
            {
                // Default format if no country code is available
                dateFormat = "yyyy-MM-dd hh:mm tt";
            }
            
            return dateTime.ToString(dateFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        internal string FormatDateOnly(DateTime dateTime)
        {
            try
            {
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                string dateFormat;
                
                if (shopDetails != null && !string.IsNullOrWhiteSpace(shopDetails.CountryCode))
                {
                    var countryCode = shopDetails.CountryCode.Trim().ToUpper();
                    
                    // Format date part based on country code (same logic as CountryBasedDateConverter)
                    // Date only, no time part
                    switch (countryCode)
                    {
                        case "LK":
                            dateFormat = "MM/dd/yyyy";
                            break;
                        case "GB":
                            dateFormat = "dd/MM/yyyy";
                            break;
                        default:
                            // Use yyyy/MM/dd format for pickup/delivery dates (as per user requirement)
                            dateFormat = "yyyy/MM/dd";
                            break;
                    }
                }
                else
                {
                    // Default format if no country code is available - use yyyy/MM/dd for pickup dates
                    dateFormat = "yyyy/MM/dd";
                }
                
                var result = dateTime.ToString(dateFormat, System.Globalization.CultureInfo.InvariantCulture);
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] FormatDateOnly: CountryCode={(shopDetails?.CountryCode ?? "null")}, Format={dateFormat}, Result={result}");
                return result;
            }
            catch (Exception ex)
            {
                // Fallback to default format if there's any error
                System.Diagnostics.Debug.WriteLine($"[ReceiptPrintingService] Error formatting date: {ex.Message}");
                return dateTime.ToString("yyyy/MM/dd", System.Globalization.CultureInfo.InvariantCulture);
            }
        }


        /// <summary>
        /// Generate the content for refund receipt
        /// </summary>
        private string GenerateRefundReceiptContent(OrderModel order, decimal refundAmount, string refundMethod, string refundReason)
        {
            var sb = new StringBuilder();
            var shop = GlobalDataService.Instance.ShopDetails;

            //Header
            //sb.AppendLine(new string('=', 40));
            sb.AppendLine("**REFUND RECEIPT**");
            sb.AppendLine(new string('-', 40));

            //Order Details (left-aligned, regular font - :REGULAR: flag for print handler)
            string OrderId = !string.IsNullOrWhiteSpace(order?.DisplayOrderId) ? order.DisplayOrderId : order?.OrderNumber ?? (order?.ApiId > 0 ? order.ApiId.ToString() : "N/A");
            sb.AppendLine($":REGULAR:Order ID: {OrderId}");
            sb.AppendLine($":REGULAR:Date & Time: {FormatDateWithTime(DateTime.Now)}");
            string orderType = order?.OrderType switch
            {
                Models.OrderType.DineIn => "DINE IN",
                Models.OrderType.TakeAway => "TAKE AWAY",
                Models.OrderType.Delivery => "DELIVERY",
                Models.OrderType.Collection => "COLLECTION",
                _ => "N/A"
            };
            if (order?.PlatformId2 == 8 || order?.PlatformId == 8)
            {
                orderType = order.TableOrderMethod?.Trim().ToUpperInvariant().Replace("-", " ").Replace("TAKEAWAY", "TAKE AWAY") ?? "";
            }
            sb.AppendLine($":REGULAR:Order Type: {orderType}");
            sb.AppendLine($":REGULAR:Customer: {order?.CustomerName ?? "N/A"}");
            sb.AppendLine($":REGULAR:Cashier: {GlobalDataService.Instance.CurrentUser?.FullName ?? "N/A"}");
            sb.AppendLine();

            //Refund Details (left-aligned, regular font - :REGULAR: flag for print handler)
            sb.AppendLine(new string('-', 40));
            sb.AppendLine($":REGULAR:Refund Amount: {shop?.Currency ?? "£"} {refundAmount:F2}");
            sb.AppendLine($":REGULAR:Refund Method: {refundMethod}");
            sb.AppendLine($":REGULAR:Reason: {refundReason ?? "N/A"}");
            sb.AppendLine(new string('-', 40));
            sb.AppendLine();

            //footer
            sb.AppendLine(CenterLine("Thank you for ordering with ", 40));
            // Tenant code (centered and bold)
            var settingsService = new SettingsService();
            var (tenantCode, _, _) = settingsService.LoadSettings();
            if (!string.IsNullOrEmpty(tenantCode))
            {
                sb.AppendLine($"**{tenantCode.ToUpper()}**");
            }
            
            // Delivery platform name + shop name (centered and bold)
            if (!string.IsNullOrEmpty(shop?.DeliveryPlatform?.Name) && !string.IsNullOrEmpty(shop?.Name))
            {
                var platformShopText = $"{shop.DeliveryPlatform.Name} _ {shop.Name}";
                int maxLineLength = 32; // depends on printer paper width (e.g. 32 chars for 58mm, 42 for 80mm)

                var words = platformShopText.Split(' ');
                var currentLine = new StringBuilder();
                var lines = new List<string>();

                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 > maxLineLength)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                    currentLine.Append(word + " ");
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine.ToString().Trim());

                // Apply bold to each line individually to ensure consistent formatting
                foreach (var line in lines)
                {
                    sb.AppendLine($"**{line}**");
                }
            }
            
            // Shop address (centered and bold, font 9) - with text wrapping
            if (!string.IsNullOrEmpty(shop?.Address))
            {
                var addressText = shop.Address;
                var maxLineLength = 32; // Same as item list margins
                
                if (addressText.Length <= maxLineLength)
                {
                    // Address fits on one line
                    sb.AppendLine($"**{addressText}**");
                }
                else
                {
                    // Split address into multiple lines
                    var words = addressText.Split(' ');
                    var currentLine = "";
                    
                    foreach (var word in words)
                    {
                        if ((currentLine + " " + word).Length <= maxLineLength)
                        {
                            currentLine += (currentLine.Length > 0 ? " " : "") + word;
                        }
                        else
                        {
                            if (currentLine.Length > 0)
                            {
                                sb.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                            else
                            {
                                // Single word is too long, break it
                                sb.AppendLine($"**{word}**");
                            }
                        }
                    }
                    
                    if (currentLine.Length > 0)
                    {
                        sb.AppendLine($"**{currentLine}**");
                    }
                }
            }
            
            // Shop contact number (centered and bold, font 9)
            if (!string.IsNullOrEmpty(shop?.ContactNo))
            {
                sb.AppendLine($"**{shop.ContactNo}**");
            }
            // Powered by Delivergate (centered and italic)
            sb.AppendLine(":ITALIC:Powered by Delivergate");
            return sb.ToString();
        }

        //CenterLine method
        private static string CenterLine(string text, int width)
        {
            if (string.IsNullOrEmpty(text) || text.Length >= width) return text ?? "";

            int pad = (width - text.Length) / 2;
            return text.PadLeft(text.Length + pad).PadRight(width);
        }

        /// <summary>
        /// Prints the refund receipt to all active printers with main receipt enabled
        /// </summary>
        public async Task PrintRefundReceiptAsync(OrderModel order, decimal refundAmount, string refundReason, string refundMode)
        {
            if (order == null) return;

            try
            {
                string refundMethod = (refundMode ?? "N/A").Trim().ToUpperInvariant();

                if (refundMethod == "MANUAL CARD") refundMethod = "CARD";
                if (refundMethod != "CASH" && refundMethod != "CARD") refundMethod = "CASH";
                

                var content = GenerateRefundReceiptContent(order, refundAmount, refundMethod, refundReason ?? "N/A");

                var printersService = PrintersService.Instance;
                foreach (var printer in printersService.Printers)
                {
                    try
                    {
                        if (!printer.IsActive) continue;
                        var printerSettings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
                        if (printerSettings == null || !printerSettings.MainReceipt) continue;

                        await PrintToPrinterAsync(printer.DeviceName, content);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PrintRefundReceiptAsync] Error printing to printer {printer.DeviceName}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintRefundReceiptAsync] Exception occurred: {ex.Message}");
            }
        }

        /// <summary>Split payment receipt strings: newline-separated, or legacy double-space-separated horizontal lines.</summary>
        private static List<string> ParseReceiptSplitPaymentLines(string paymentMethod)
        {
            if (string.IsNullOrWhiteSpace(paymentMethod)) return new List<string>();
            var raw = paymentMethod.Trim();
            var lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            if (lines.Count <= 1 && raw.Contains("  ", StringComparison.Ordinal))
            {
                lines = raw.Split(new[] { "  " }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .ToList();
            }
            return lines;
        }

        /// <summary>Each split row uses the same <c>**PAYMENT:**|**…**</c> pipe layout as a single-method receipt line.</summary>
        private static void AppendSplitPaymentRowsSameStyleAsSingle(StringBuilder receipt, IReadOnlyList<string> splitLines)
        {
            foreach (var line in splitLines)
            {
                var trimmed = (line ?? string.Empty).Trim();
                if (trimmed.Length == 0) continue;
                receipt.AppendLine($"**PAYMENT:**|**{trimmed.ToUpperInvariant()}**");
            }
        }

        private string GenerateCartReceiptContent(CartService cartService, CardTransactionResult cardTransaction = null, string paymentMethod = null, bool includeOrderPlacedLine = true)
        {
            var receipt = new StringBuilder();
            var shopDetails = GlobalDataService.Instance.ShopDetails;
            
            // Shop logo marker (if logo exists) - will be rendered by PrintToPrinterAsync
            if (!string.IsNullOrWhiteSpace(shopDetails?.ShopLogo))
            {
                receipt.AppendLine($":LOGO:{shopDetails.ShopLogo}");
            }
            
            // Delivery Platform Name (left) and Order Display ID (right) on same line
            if (!string.IsNullOrEmpty(shopDetails?.DeliveryPlatform?.Name))
            {
                var platformName = shopDetails.DeliveryPlatform.Name;
                var orderId = cartService.DisplayOrderId ?? "N/A";

                int maxLeftTextLength = 11;
                if (platformName.Length <= maxLeftTextLength)
                {
                    // Fits on one line
                    receipt.AppendLine($"**{platformName}**|**{orderId}**");
                }
                else
                {
                    // Split by words to avoid breaking words
                    var words = platformName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var firstLine = "";
                    int wordIndex = 0;
                    
                    // Try to fit as many complete words as possible on first line
                    for (int i = 0; i < words.Length; i++)
                    {
                        var candidate = firstLine.Length == 0 ? words[i] : firstLine + " " + words[i];
                        if (candidate.Length <= maxLeftTextLength)
                        {
                            firstLine = candidate;
                            wordIndex = i + 1;
                        }
                        else
                        {
                            // Word doesn't fit, stop here
                            break;
                        }
                    }
                    
                    // Print first line with order ID
                    if (!string.IsNullOrEmpty(firstLine))
                    {
                        receipt.AppendLine($"**{firstLine}**|**{orderId}**");
                    }
                    else
                    {
                        // If first word is too long, put order ID on its own line
                        receipt.AppendLine($"**{orderId}**");
                        wordIndex = 0; // Start from beginning
                    }
                    
                    // Print remaining words, wrapping at word boundaries
                    var currentLine = "";
                    for (int i = wordIndex; i < words.Length; i++)
                    {
                        var word = words[i];
                        if (currentLine.Length == 0)
                        {
                            currentLine = word;
                        }
                        else
                        {
                            var candidate = currentLine + " " + word;
                            if (candidate.Length <= maxLeftTextLength)
                            {
                                currentLine = candidate;
                            }
                            else
                            {
                                // Current line is full, print it and start new line
                                receipt.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                        }
                    }
                    
                    // Print any remaining content
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        receipt.AppendLine($"**{currentLine}**");
                    }
                }
            }
            
            // Order placement time (centered) - omitted when printing from cart (pre-order) print button
            var orderTime = cartService.OrderCreatedAt ?? DateTime.Now;
            if (includeOrderPlacedLine)
            {    
                var orderTimeText = $"Order placed: {FormatDateWithTime(orderTime)}";
                receipt.AppendLine(orderTimeText);
            }
            
            // Order Type (centered with big bold letters)
            var orderType = cartService.OrderType ?? "Take Away";
            
            // For Dine In orders, print only the table name when available (do NOT print number)
            if (orderType.ToLower() == "dine in")
            {
                var tableName = cartService.TableName;
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    receipt.AppendLine($"**Table: {tableName}**");
                }
            }
            else if (orderType.ToLower() != "dine in")
            {
            // Pickup date line (centered) - use pickup time if available, otherwise use order time
            var pickupDateTime = cartService.PickupTime.HasValue ? cartService.PickupTime.Value : orderTime;
            var pickupLabel = orderType.ToLower() == "delivery" ? "Delivery" : "Pickup";
            var pickupDateText = $"{pickupLabel} - {FormatDateOnly(pickupDateTime)} at";
            receipt.AppendLine(pickupDateText);
            
            // Pickup time line (centered and bold) - use pickup time if available, otherwise use order time
            var pickupTimeText = $"**{pickupDateTime:hh:mm tt}**";
            receipt.AppendLine(pickupTimeText);
            }
            
            // Order Type (centered with big bold letters) - moved below pickup/table section
            receipt.AppendLine($"**{orderType.ToUpper()}**");

            // Delivery address (for Delivery orders) printed right after order type, bold normal size, no header
            if (orderType.ToLower() == "delivery" && !string.IsNullOrWhiteSpace(cartService.DeliveryAddress))
            {
                var deliveryAddress = cartService.DeliveryAddress;
                var maxLineLength = 32; // Same as item list margins
                if (deliveryAddress.Length <= maxLineLength)
                {
                    receipt.AppendLine($"**{deliveryAddress}**");
                }
                else
                {
                    var words = deliveryAddress.Split(' ');
                    var currentLine = "";
                    foreach (var word in words)
                    {
                        if ((currentLine + " " + word).Length <= maxLineLength)
                        {
                            currentLine += (currentLine.Length > 0 ? " " : "") + word;
                        }
                        else
                        {
                            if (currentLine.Length > 0)
                            {
                                receipt.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                            else
                            {
                                receipt.AppendLine($"**{word}**");
                            }
                        }
                    }
                    if (currentLine.Length > 0)
                    {
                        receipt.AppendLine($"**{currentLine}**");
                    }
                }
            }
            
            // Separator line after order type (bold and full width) - with small left offset
            var separatorLine = "".PadRight(50, '-');
            var leftOffset = 2; // Small left offset
            receipt.AppendLine("**" + separatorLine.PadLeft(separatorLine.Length + leftOffset) + "**");
            
            // Customer info

            var nameForReceipt = !string.IsNullOrWhiteSpace(cartService.CustomerName) ? cartService.CustomerName : cartService.LastCustomerName;
            var phoneForReceipt = !string.IsNullOrWhiteSpace(cartService.CustomerPhone) ? cartService.CustomerPhone : cartService.LastCustomerPhone;

            // Customer Name (centered and bold)
            if (!string.IsNullOrEmpty(nameForReceipt))
            {
                receipt.AppendLine($"**{nameForReceipt}**");
            }
            
            // Customer Phone (centered, not bold) - skip for Guest customer
            var isGuestCustomer = string.Equals(nameForReceipt?.Trim(), "Guest Customer", StringComparison.OrdinalIgnoreCase);
            // skip phone for POS non delivery orders (Cart only for POS orders)
            var isNonDeliveryOrder = !string.Equals(cartService.OrderType?.Trim(), "Delivery", StringComparison.OrdinalIgnoreCase);
            if (!isGuestCustomer && !isNonDeliveryOrder && !string.IsNullOrEmpty(phoneForReceipt))
            {
                receipt.AppendLine($"Contact: {phoneForReceipt}");
            }
             
            // (Delivery address already printed after order type when applicable)
             
             // Separator line after customer info (bold and full width) - with small left offset
             var phoneSeparatorLine = "".PadRight(50, '-');
             receipt.AppendLine("**" + phoneSeparatorLine.PadLeft(phoneSeparatorLine.Length + leftOffset) + "**");
            
            // Order Items Section
            foreach (var item in cartService.OrderItems)
            {
                // Main item with quantity and price - optimized for 80mm printer
                 var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                var priceLine = $"{shopDetails?.Currency ?? "£"} {item.Total:F2}";
                 AppendItemLineWithPrice(receipt, itemLine, priceLine);
                
                // Modifiers - indented and left-aligned like in cart
                if (item.ModifierDetailsForDisplay?.Any() == true)
                {
                    foreach (var modifier in item.ModifierDetailsForDisplay)
                    {
                        var modifierLine = $"  {modifier.ModifierName}";
                        if (modifier.Price > 0)
                        {
                            modifierLine += $" +{shopDetails?.Currency ?? "£"} {modifier.Price:F2}";
                        }
                        receipt.AppendLine(modifierLine);
                    }
                }
                
                // Item-level discount - print below item if present (total discount, not per-item)
                if (item.VisibleDiscountAmount > 0m)
                {
                    receipt.AppendLine($"  Discount : {shopDetails?.Currency ?? "£"} {item.VisibleDiscountAmount:F2}");
                }
                
                // Item note
                if (!string.IsNullOrEmpty(item.Note))
                {
                    receipt.AppendLine($"  Note: {item.Note}");
                }
                
                receipt.AppendLine();
            }

                          // Separator before total - with small left offset (same format as other separators)
             var totalSeparatorLine = "".PadRight(50, '-');
             receipt.AppendLine("**" + totalSeparatorLine.PadLeft(totalSeparatorLine.Length + leftOffset) + "**");
             
            // Order Notes (if available) - after separator but before total
            if (!string.IsNullOrEmpty(cartService.Note))
            {
                // Emit a single logical line; printer routine will wrap and center
                var noteContent = (cartService.Note ?? string.Empty).Trim();
                receipt.AppendLine($"**Order Note:** {noteContent}");

                // Separator line after order notes (bold and full width) - with small left offset
                var orderNoteSeparatorLine = "".PadRight(50, '-');
                receipt.AppendLine("**" + orderNoteSeparatorLine.PadLeft(orderNoteSeparatorLine.Length + leftOffset) + "**");
            }

            // Allergy info message (hardcoded for all receipts) - always appears
            receipt.AppendLine("**IMPORTANT:** Call the restaurant for allergy info");

            // Separator line after allergy info (bold and full width) - with small left offset
            var allergySeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + allergySeparatorLine.PadLeft(allergySeparatorLine.Length + leftOffset) + "**");
             
                                     // Subtotal
            var subtotal = cartService.Total;
            receipt.AppendLine($"**SUBTOTAL:**|**{shopDetails?.Currency ?? "£"} {subtotal:F2}**");
            
            // Discount (if any)
            if (cartService.DiscountAmount > 0)
            {
                // Include discount percentage if it's a percentage-based discount
                string discountLabel = "DISCOUNT:";
                if (cartService.DiscountPercent > 0)
                {
                    discountLabel = $"DISCOUNT ({cartService.DiscountPercent}%):";
                }
                receipt.AppendLine($"**{discountLabel}**|**{shopDetails?.Currency ?? "£"} {cartService.DiscountAmount:F2}**");
            }
            
            // Coupon (if any) - use voucher description format
            if (cartService.CouponAmount > 0)
            {
                string couponLabel = "COUPON:";
                // Use CouponDescription if available, otherwise build from vouchers
                
                if (cartService.Vouchers != null && cartService.Vouchers.Count > 0)
                {
                    couponLabel = $"{FormatVoucherDescription(cartService.Vouchers, cartService.CouponCode).ToUpper()}:";
                }
                else if (!string.IsNullOrWhiteSpace(cartService.CouponDescription))
                {
                    couponLabel = $"{cartService.CouponDescription.ToUpper()}:";
                }
                receipt.AppendLine($"**{couponLabel}**|**{shopDetails?.Currency ?? "£"} {cartService.CouponAmount:F2}**");
            }

            // Shop Fees (if any) - use cart-calculated fees so removals/order type filters apply
            try
            {
                var calculatedFees = cartService.GetCalculatedShopFees();
                if (calculatedFees != null && calculatedFees.Count > 0 && (cartService.OrderItems?.Count ?? 0) > 0)
                {
                    foreach (var fee in calculatedFees)
                    {
                        if (fee == null || fee.Amount <= 0) continue;

                        var labelName = string.IsNullOrWhiteSpace(fee.Name) ? "Shop Fee" : fee.Name;
                        
                        // Check if fee_name is "Service charge" and adjust label based on mandatory flag
                        // For POS orders (platform ID 9), show "Mandatory Service Charge" or "Discretionary Service Charge"
                        // For platform ID not 9, show "Service Charge"
                        if (string.Equals(labelName, "Service charge", StringComparison.OrdinalIgnoreCase))
                        {
                            // Cart receipts are POS orders ( platform ID 9), so show mandatory/discretionary labels
                            labelName = fee.IsMandatory ? "Mandatory Service Charge" : "Discretionary Service Charge";
                        }
                        
                        var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                        string displayLabel;
                        
                        if (type == "PERCENTAGE")
                        {
                            // For percentage fees, show the percentage value
                            decimal percentValue = fee.FeeValue;
                            
                            // If FeeValue not set, try to look it up from shop details
                            if (percentValue <= 0 && fee.ShopFeeId > 0 && shopDetails?.ShopFees != null)
                            {
                                var shopFee = shopDetails.ShopFees.FirstOrDefault(sf => sf.Id == fee.ShopFeeId);
                                if (shopFee != null)
                                {
                                    percentValue = shopFee.Fee;
                                }
                            }
                            
                            if (percentValue > 0)
                            {
                                displayLabel = $"{labelName}({percentValue:0.##}%)";
                            }
                            else
                            {
                                // Last resort fallback
                                displayLabel = $"{labelName}(%)";
                            }
                        }
                        else if (type == "VALUE")
                        {
                            // For value fees, show "(value)" in bracket
                            displayLabel = $"{labelName}(value)";
                        }
                        else
                        {
                            // Unknown type, just show the name
                            displayLabel = labelName;
                        }
                        
                        receipt.AppendLine($"**{displayLabel}:**|**{shopDetails?.Currency ?? "£"} {fee.Amount:F2}**");
                    }
                }
            }
            catch { }
            
            // Delivery Charge (if any)
            if (cartService.DeliveryCharge > 0)
            {
                var chargeLabel = "DELIVERY CHARGE";
                receipt.AppendLine($"**{chargeLabel}:**|**{shopDetails?.Currency ?? "£"} {cartService.DeliveryCharge:F2}**");
            }
            
            // Total (Subtotal - Discount - Coupon + Delivery)
            var finalTotal = cartService.SubTotal;
            receipt.AppendLine($"**TOTAL:**|**{shopDetails?.Currency ?? "£"} {finalTotal:F2}**");
            
            // Tax summary (after total)
            var summaryRows = cartService.CurrentTaxResult?.SummaryRows;
            var hasShopTaxProfiles = (shopDetails?.TaxProfiles?.Count ?? 0) > 0;
            if (hasShopTaxProfiles && summaryRows != null && summaryRows.Count > 0)
            {
                AppendTaxSummaryBlock(receipt, summaryRows, shopDetails, leftOffset);
            }
            
            // Payment section: only when this is a completed payment (not cart-only print from print button)
            var hasPayment = cardTransaction != null || !string.IsNullOrEmpty(paymentMethod);
            if (hasPayment)
            {
                // If paying with CASH, show tendered and balance in same style
                var lastCashGiven = POS_UI.Services.GlobalDataService.Instance.LastCashGiven;
                var lastCashBalance = POS_UI.Services.GlobalDataService.Instance.LastCashBalance;
                var paymentMethodNormalized = (paymentMethod ?? string.Empty).Trim().ToUpperInvariant();
                if (paymentMethodNormalized == "CASH" && lastCashGiven.HasValue)
                {
                    receipt.AppendLine($"**PAID AMOUNT:**|**{shopDetails?.Currency ?? "£"} {lastCashGiven.Value:F2}**");
                    var balanceToShow = Math.Max(lastCashBalance ?? 0m, 0m);
                    receipt.AppendLine($"**CASH BALANCE:**|**{shopDetails?.Currency ?? "£"} {balanceToShow:F2}**");
                }
                
                // Separator line after total (bold and full width) - with small left offset
                var totalAfterSeparatorLine = "".PadRight(50, '-');
                receipt.AppendLine("**" + totalAfterSeparatorLine.PadLeft(totalAfterSeparatorLine.Length + leftOffset) + "**");
                
                // Normalize payment method: treat ManualCard as CARD for display.
                // Split payments: newline (or legacy "  ") separated — print one method/amount per line.
                if (!string.IsNullOrEmpty(paymentMethod))
                {
                    var splitLines = ParseReceiptSplitPaymentLines(paymentMethod);
                    if (splitLines.Count > 1)
                    {
                        AppendSplitPaymentRowsSameStyleAsSingle(receipt, splitLines);
                    }
                    else
                    {
                        var pm = splitLines.Count == 1 ? splitLines[0] : paymentMethod.Trim();
                        var pmNormalizedForDisplay = pm.ToUpperInvariant();

                        if (pmNormalizedForDisplay == "MANUALCARD" || pmNormalizedForDisplay == "MANUAL CARD")
                        {
                            pm = "CARD";
                        }
                        else if (pmNormalizedForDisplay == "COD")
                        {
                            pm = "Pay on Delivery";
                        }
                        else if (pmNormalizedForDisplay == "COT")
                        {
                            pm = "Pay on Takeaway";
                        }

                        receipt.AppendLine($"**PAYMENT:**|**{pm.ToUpperInvariant()}**");
                    }
                }
                
                // Separator line after payment method (bold and full width) - with small left offset
                var paymentSeparatorLine = "".PadRight(50, '-');
                receipt.AppendLine("**" + paymentSeparatorLine.PadLeft(paymentSeparatorLine.Length + leftOffset) + "**");
            }
            
            // Cashier near footer, above thank you, to keep it visible but unobtrusive
            var footerCashierCart = GlobalDataService.Instance.CurrentUser?.FullName;
            if (!string.IsNullOrWhiteSpace(footerCashierCart))
            {
                receipt.AppendLine($"Cashier: {footerCashierCart}");
            }
            
            // Thank you message
            receipt.AppendLine("Thank you for ordering with");
            
            // Tenant code (centered and bold)
            var settingsService = new SettingsService();
            var (tenantCode, _, _) = settingsService.LoadSettings();

            //brand name from shop details
            var brandName = shopDetails?.DeliveryPlatform?.BrandName;
            /*if (!string.IsNullOrEmpty(brandName))
            {
                receipt.AppendLine($"**{brandName.ToUpper()}**");
            }*/
            
            // Delivery platform name + shop name (centered and bold)
            if (!string.IsNullOrEmpty(brandName) && !string.IsNullOrEmpty(shopDetails?.Name))
            {
                var platformShopText = $"{brandName} - {shopDetails.Name}";
                int maxLineLength = 32; // depends on printer paper width (e.g. 32 chars for 58mm, 42 for 80mm)

                var words = platformShopText.Split(' ');
                var currentLine = new StringBuilder();
                var lines = new List<string>();

                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 > maxLineLength)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                    currentLine.Append(word + " ");
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine.ToString().Trim());

                // Apply bold to each line individually to ensure consistent formatting
                foreach (var line in lines)
                {
                    receipt.AppendLine($"**{line}**");
                }
            }
            
            // Shop address (centered and bold, font 9) - with text wrapping
            if (!string.IsNullOrEmpty(shopDetails?.Address))
            {
                var addressText = shopDetails.Address;
                var maxLineLength = 32; // Same as item list margins
                
                if (addressText.Length <= maxLineLength)
                {
                    // Address fits on one line
                    receipt.AppendLine($"**{addressText}**");
                }
                else
                {
                    // Split address into multiple lines
                    var words = addressText.Split(' ');
                    var currentLine = "";
                    
                    foreach (var word in words)
                    {
                        if ((currentLine + " " + word).Length <= maxLineLength)
                        {
                            currentLine += (currentLine.Length > 0 ? " " : "") + word;
                        }
                        else
                        {
                            if (currentLine.Length > 0)
                            {
                                receipt.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                            else
                            {
                                // Single word is too long, break it
                                receipt.AppendLine($"**{word}**");
                            }
                        }
                    }
                    
                    if (currentLine.Length > 0)
                    {
                        receipt.AppendLine($"**{currentLine}**");
                    }
                }
            }
            
            // Shop contact number (centered and bold, font 9)
            if (!string.IsNullOrEmpty(shopDetails?.ContactNo))
            {
                receipt.AppendLine($"**{shopDetails.ContactNo}**");
            }

            var taxRegNo = shopDetails?.TaxRegNo?.Trim();
            if (!string.IsNullOrEmpty(taxRegNo))
            {
                // Single line, no | — PrintToPrinterAsync draws this centered (tax reg branch)
                receipt.AppendLine($"**TAX REG NO: {taxRegNo}**");
            }
            // Powered by Delivergate (centered and italic)
            receipt.AppendLine(":ITALIC:Powered by Delivergate");

            return receipt.ToString();
        }

        private string GenerateOrderReceiptContent(OrderModel order, string paymentMethod = null)
        {
            var receipt = new StringBuilder();
            var shopDetails = GlobalDataService.Instance.ShopDetails;

            // Shop logo marker (if logo exists) - will be rendered by PrintToPrinterAsync
            if (!string.IsNullOrWhiteSpace(shopDetails?.ShopLogo))
            {
                receipt.AppendLine($":LOGO:{shopDetails.ShopLogo}");
            }

           // Delivery Platform Name (left) and Order Display ID (right) on same line
            var platformName = order?.DeliveryPlatfornName;
            // Fallback to PlatformName if DeliveryPlatfornName is empty
            if (string.IsNullOrEmpty(platformName))
            {
                platformName = order?.PlatformName;
            }
            // Final fallback to shop platform name if still empty and not an incoming order
            if (string.IsNullOrEmpty(platformName) && (order?.ApiId == null || order.ApiId == 0))
            {
                platformName = shopDetails?.DeliveryPlatform?.Name;
            }
            var orderIdHeader = order?.DisplayOrderId ?? order?.OrderNumber ?? ((order?.ApiId ?? 0) > 0 ? order.ApiId.ToString() : null);
            if (!string.IsNullOrEmpty(platformName) || !string.IsNullOrEmpty(orderIdHeader))
            {
                // If platform is still empty, keep it blank but don't suppress the order id
                var leftText = platformName ?? string.Empty;
                var rightText = string.IsNullOrEmpty(orderIdHeader) ? "N/A" : orderIdHeader;
                int maxLeftTextLength = 11;
                if (leftText.Length <= maxLeftTextLength)
                {
                    // Fits on one line
                    receipt.AppendLine($"**{leftText}**|**{rightText}**");
                }
                else
                {
                    // Split by words to avoid breaking words
                    var words = leftText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    var firstLine = "";
                    int wordIndex = 0;
                    
                    // Try to fit as many complete words as possible on first line
                    for (int i = 0; i < words.Length; i++)
                    {
                        var candidate = firstLine.Length == 0 ? words[i] : firstLine + " " + words[i];
                        if (candidate.Length <= maxLeftTextLength)
                        {
                            firstLine = candidate;
                            wordIndex = i + 1;
                        }
                        else
                        {
                            // Word doesn't fit, stop here
                            break;
                        }
                    }
                    
                    // Print first line with order ID
                    if (!string.IsNullOrEmpty(firstLine))
                    {
                        receipt.AppendLine($"**{firstLine}**|**{rightText}**");
                    }
                    else
                    {
                        // If first word is too long, put order ID on its own line
                        receipt.AppendLine($"**{rightText}**");
                        wordIndex = 0; // Start from beginning
                    }
                    
                    // Print remaining words, wrapping at word boundaries
                    var currentLine = "";
                    for (int i = wordIndex; i < words.Length; i++)
                    {
                        var word = words[i];
                        if (currentLine.Length == 0)
                        {
                            currentLine = word;
                        }
                        else
                        {
                            var candidate = currentLine + " " + word;
                            if (candidate.Length <= maxLeftTextLength)
                            {
                                currentLine = candidate;
                            }
                            else
                            {
                                // Current line is full, print it and start new line
                                receipt.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                        }
                    }
                    
                    // Print any remaining content
                    if (!string.IsNullOrEmpty(currentLine))
                    {
                        receipt.AppendLine($"**{currentLine}**");
                    }
                }
            }

            // Order placement time (centered)
            var orderTime = order?.CreatedAt ?? DateTime.Now;
            var orderTimeText = $"Order placed: {FormatDateWithTime(orderTime)}";
            receipt.AppendLine(orderTimeText);

            // Order Type (centered with big bold letters)
            var orderTypeEnum = order?.OrderType ?? Models.OrderType.TakeAway;
            var orderTypeText = orderTypeEnum switch
            {
                Models.OrderType.DineIn => "Dine In",
                Models.OrderType.TakeAway => "Take Away",
                Models.OrderType.Delivery => "Delivery",
                _ => "Take Away"
            };

            // For Dine In orders, show table number instead of pickup time
            if (orderTypeText.ToLower() == "dine in" || (order.PlatformId2 == 8 && !string.IsNullOrWhiteSpace(order.TableName)))
            {
                // No numeric table output here; handled below as name-only before order type
            }
            else if (orderTypeText.ToLower() != "dine in")
            {
                // Debug: show which delivery time fields are used
                System.Diagnostics.Debug.WriteLine($"[Receipt] delivery_date_time Scheduled={order?.ScheduledTime:o} DeliveryDateTime={order?.DeliveryDateTime:o} CreatedAt={orderTime:o}");
                // Pickup/Delivery date/time (centered)
                var pickupSrc = order?.ScheduledTime ?? order?.DeliveryDateTime ?? orderTime;
                // For PHP incoming orders: print exactly as provided; for others keep prior behavior
                DateTime pickupTime;
                if (order?.IsFromPhpApi == true)
                {
                    pickupTime = pickupSrc;
                }
                else
                {
                    try
                    {
                        if (pickupSrc.Kind == DateTimeKind.Local)
                        {
                            pickupTime = pickupSrc.ToUniversalTime();
                        }
                        else if (pickupSrc.Kind == DateTimeKind.Unspecified && order?.DeliveryDateTime.HasValue == true)
                        {
                            // Treat as local if unspecified and convert to UTC
                            pickupTime = DateTime.SpecifyKind(pickupSrc, DateTimeKind.Local).ToUniversalTime();
                        }
                        else
                        {
                            pickupTime = pickupSrc; // already UTC or unspecified without context
                        }
                    }
                    catch { pickupTime = pickupSrc; }
                }
                var pickupLabel = orderTypeText.ToLower() == "delivery" ? "Delivery" : "Pickup";
                var pickupDateText = $"{pickupLabel} - {FormatDateOnly(pickupTime)} at";
                receipt.AppendLine(pickupDateText);

                var pickupTimeText = $"**{pickupTime:hh:mm tt}**";
                receipt.AppendLine(pickupTimeText);
            }

            // For Dine In / table order, show table name (before order type). Include platform 8 so POS-selected table name is shown.
            var isDineInOrTableOrder = string.Equals(orderTypeText, "Dine In", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(order?.Platform, "Table order", StringComparison.OrdinalIgnoreCase) && string.Equals(order?.TableOrderMethod, "Dine-in", StringComparison.OrdinalIgnoreCase))
                || (order != null && (order.PlatformId == 8 || order.PlatformId2 == 8));
            if (isDineInOrTableOrder)
            {
                var resolvedTableName = !string.IsNullOrWhiteSpace(order?.TableName)
                    ? order.TableName
                    : POS_UI.Services.GlobalDataService.Instance?.CurrentOrderForEdit?.TableName;

                if (!string.IsNullOrWhiteSpace(resolvedTableName))
                {
                    receipt.AppendLine($"**Table: {resolvedTableName}**");
                }
            }

            // Order Type (centered with big bold letters). For platform 8 (table order), print TableOrderMethod when present.
            var orderTypeTextforPrint = orderTypeText.ToUpper();
            if (order.PlatformId == 8 || order.PlatformId2 == 8 || string.Equals(order.Platform, "Table order", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(order?.TableOrderMethod))
                {
                    orderTypeTextforPrint = order.TableOrderMethod.Trim().ToUpperInvariant().Replace("-", " ").Replace("TAKEAWAY", "TAKE AWAY");
                }
                else
                {
                    orderTypeTextforPrint = "DINEIN";
                }
            }
            receipt.AppendLine($"**{orderTypeTextforPrint}**");

            // Delivery address (for Delivery orders) - print in one line if it fits; wrap only when needed
            if (orderTypeText.ToLower() == "delivery" && !string.IsNullOrWhiteSpace(order?.DeliveryAddress))
            {
                var deliveryAddress = order.DeliveryAddress.Trim();
                var maxLineLength = 32;

                // If the entire address fits, print as a single line
                if (deliveryAddress.Length <= maxLineLength)
                {
                    receipt.AppendLine($"**{deliveryAddress}**");
                }
                else
                {
                    // Prefer breaking at commas, but only when necessary; further wrap long parts by words
                    var segments = deliveryAddress.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(s => s.Trim())
                                                  .ToList();

                    if (segments.Count == 0)
                    {
                        segments.Add(deliveryAddress);
                    }

                    var currentLine = string.Empty;
                    Action<string> flushLine = line =>
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            receipt.AppendLine($"**{line.Trim()}**");
                        }
                    };

                    foreach (var segment in segments)
                    {
                        var candidate = string.IsNullOrEmpty(currentLine) ? segment : currentLine + ", " + segment;
                        if (candidate.Length <= maxLineLength)
                        {
                            currentLine = candidate;
                        }
                        else
                        {
                            // Flush current line if it has content
                            flushLine(currentLine);

                            // Now wrap the segment itself by words if it still exceeds
                            if (segment.Length <= maxLineLength)
                            {
                                currentLine = segment;
                            }
                            else
                            {
                                var words = segment.Split(' ');
                                var innerLine = string.Empty;
                                foreach (var word in words)
                                {
                                    var innerCandidate = string.IsNullOrEmpty(innerLine) ? word : innerLine + " " + word;
                                    if (innerCandidate.Length <= maxLineLength)
                                    {
                                        innerLine = innerCandidate;
                                    }
                                    else
                                    {
                                        flushLine(innerLine);
                                        innerLine = word;
                                    }
                                }
                                // After wrapping this long segment, start a new currentLine
                                currentLine = innerLine;
                            }
                        }
                    }

                    // Flush any remaining text
                    flushLine(currentLine);
                }
            }

            // Separator line after order type (bold and full width) - with small left offset
            var separatorLine = "".PadRight(50, '-');
            var leftOffset = 2;
            receipt.AppendLine("**" + separatorLine.PadLeft(separatorLine.Length + leftOffset) + "**");

            // Customer info
            if (!string.IsNullOrEmpty(order?.CustomerName))
            {
                receipt.AppendLine($"**{order.CustomerName}**");
            }
            // Skip phone for Guest customer
            var isGuestCustomer = string.Equals(order?.CustomerName?.Trim(), "Guest Customer", StringComparison.OrdinalIgnoreCase);
            // skip phone for POS non delivery orders
            var isPosNonDeliveryOrder = (order.PlatformId2 == 9 || order.PlatformId == 9) && order?.OrderType != Models.OrderType.Delivery;
            if (!isGuestCustomer && !isPosNonDeliveryOrder && !string.IsNullOrEmpty(order?.CustomerPhone))
            {
                receipt.AppendLine($"Contact: {order.CustomerPhone}");
            }

            // Separator after customer info
            var phoneSeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + phoneSeparatorLine.PadLeft(phoneSeparatorLine.Length + leftOffset) + "**");

            // Items
            if (order?.Items != null)
            {
                foreach (var item in order.Items)
                {
                    var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                    // Prefer API-provided total (already includes modifiers) to match modal
                    decimal lineTotal = 0m;
                    if (item.ApiItemPrice > 0)
                    {
                        lineTotal = item.ApiItemPrice;
                    }
                    else if (item.Total > 0)
                    {
                        lineTotal = item.Total;
                    }
                    else if (item.Price > 0 && item.Quantity > 0)
                    {
                        lineTotal = item.Price * item.Quantity;
                    }
                    var priceLine = $"{shopDetails?.Currency ?? "£"} {lineTotal:F2}";
                    AppendItemLineWithPrice(receipt, itemLine, priceLine);

                    // Prefer ExternalModifierDetailsForDisplay when set (incoming orders), otherwise fallback
                    var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                        ? item.ExternalModifierDetailsForDisplay
                        : item.ModifierDetailsForDisplay;

                    if (modifiersToPrint?.Any() == true)
                    {
                        foreach (var modifier in modifiersToPrint)
                        {
                            var indentText = modifier.Indentation ?? string.Empty;
                            var nameText = (modifier.IsNested ? "↳ " : string.Empty) + modifier.ModifierName;
                            var modifierLine = $"  {indentText}{nameText}";
                            if (modifier.Price > 0)
                            {
                                modifierLine += $" +{shopDetails?.Currency ?? "£"} {modifier.Price:F2}";
                            }
                            receipt.AppendLine(modifierLine);
                        }
                    }

                    // Item-level discount - print below item if present
                    var orderItemDiscount = GetOrderItemDisplayDiscount(item, order: order);
                    if (orderItemDiscount > 0m)
                    {
                        receipt.AppendLine($"  Discount : {shopDetails?.Currency ?? "£"} {orderItemDiscount:F2}");
                    }

                    if (!string.IsNullOrEmpty(item.Note))
                    {
                        receipt.AppendLine($"  Note: {item.Note}");
                    }

                    receipt.AppendLine();
                }
            }

            // Separator before totals
            var totalSeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + totalSeparatorLine.PadLeft(totalSeparatorLine.Length + leftOffset) + "**");

            // Order Notes
            if (!string.IsNullOrEmpty(order?.OrderNotes))
            {
                var noteContent = (order.OrderNotes ?? string.Empty).Trim();
                receipt.AppendLine($"**Order Note:** {noteContent}");

                var orderNoteSeparatorLine = "".PadRight(50, '-');
                receipt.AppendLine("**" + orderNoteSeparatorLine.PadLeft(orderNoteSeparatorLine.Length + leftOffset) + "**");
            }

            // Allergy info
            receipt.AppendLine("**IMPORTANT:** Call the restaurant for allergy info");

            var allergySeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + allergySeparatorLine.PadLeft(allergySeparatorLine.Length + leftOffset) + "**");

            // Subtotal and totals (prefer API values)
            var subtotal = order?.ApiSubTotal ?? order?.Items?.Sum(i => i.Total) ?? 0m;
            receipt.AppendLine($"**SUBTOTAL:**|**{shopDetails?.Currency ?? "£"} {subtotal:F2}**");

            //receipt.AppendLine($"**DISCOUNT:**|**{shopDetails?.Currency ?? "£"} {order.DiscountAmount:F2}**");
            if ((order?.DiscountAmount ?? 0m) > 0m)
            {
                // Include discount percentage if it's a percentage-based discount
                string discountLabel = "DISCOUNT:";
                if (order.DiscountModeApplied == "percentage" && order.DiscountPercentage > 0)
                {
                    discountLabel = $"DISCOUNT ({order.DiscountPercentage}%):";
                }
                receipt.AppendLine($"**{discountLabel}**|**{shopDetails?.Currency ?? "£"} {order.DiscountAmount:F2}**");
            }

            // Reward (loyalty) discount
            if ((order?.RewardDiscount ?? 0m) > 0m)
            {
                receipt.AppendLine($"**REWARD DISCOUNT:**|**{shopDetails?.Currency ?? "£"} {order.RewardDiscount:F2}**");
            }

            // BOGO Discount (if any)
            if ((order?.BogoDiscount ?? 0m) > 0m)
            {
                var bogoLabel = "BOGO DISCOUNT:";
                var bogoValue = $"{shopDetails?.Currency ?? "£"} {order.BogoDiscount:F2}";
                receipt.AppendLine($"**{bogoLabel}**|**{bogoValue}**");
            }

            // Coupon or Voucher (check both as voucher may be stored in VoucherDiscount)
            var VoucherAmount = order.VoucherDiscount;
            if (VoucherAmount > 0m)
            {
                string couponLabel = "COUPON:";
                // Build voucher description from order vouchers
                if (order.Vouchers != null && order.Vouchers.Count > 0)
                {
                    couponLabel = $"{FormatVoucherDescription(order.Vouchers, order.CouponCode).ToUpper()}:";
                }
                else if (!string.IsNullOrWhiteSpace(order.CouponCode))
                {
                    couponLabel = $"COUPON ({order.CouponCode}):";
                }
                receipt.AppendLine($"**{couponLabel}**|**{shopDetails?.Currency ?? "£"} {VoucherAmount:F2}**");
            }

            if ((order?.DeliveryCharge ?? 0m) > 0m)
            {
                var chargeLabel = "DELIVERY CHARGE";
                receipt.AppendLine($"**{chargeLabel}:**|**{shopDetails?.Currency ?? "£"} {order.DeliveryCharge:F2}**");
            }

            // Shop Fees (if any) - print individual fee names like modal; default to "Shop Fee"
            try
            {
                if (order?.OrderShopFees != null && order.OrderShopFees.Count > 0)
                {
                    // Filter fees relevant to this order type if needed, but OrderShopFees should already be filtered
                    // Use IsApplicable logic if OrderShopFees contains all fees
                    
                    foreach (var fee in order.OrderShopFees)
                    {
                        if (fee == null || fee.Amount <= 0) continue;
                        
                        // Check if fee is applicable for current order type
                        if (!string.IsNullOrEmpty(fee.Type) && order.OrderType != OrderType.DineIn && !string.IsNullOrEmpty(fee.Type) && !fee.Type.Trim().Equals("ALL", StringComparison.OrdinalIgnoreCase))
                        {
                             var currentOrderType = order.OrderType;
                             var feeType = fee.Type.Trim();
                             
                             bool isApplicable = false;
                             if (feeType.Equals("TAKEAWAY", StringComparison.OrdinalIgnoreCase) && (currentOrderType == OrderType.TakeAway || currentOrderType == OrderType.Collection)) isApplicable = true;
                             else if (feeType.Equals("DELIVERY", StringComparison.OrdinalIgnoreCase) && currentOrderType == OrderType.Delivery) isApplicable = true;
                             else if (feeType.Equals("DINEIN", StringComparison.OrdinalIgnoreCase) && currentOrderType == OrderType.DineIn) isApplicable = true;
                             
                             if (!isApplicable) continue;
                        }

                        var labelName = string.IsNullOrWhiteSpace(fee.Name) ? "Shop Fee" : fee.Name;
                        
                        // Determine mandatory status - use shop details as fallback if API value might be incorrect
                        bool isMandatory = fee.IsMandatory;
                        if (shopDetails?.ShopFees != null)
                        {
                            // Try to find matching shop fee by ID first, then by name
                            var shopFee = fee.ShopFeeId > 0 
                                ? shopDetails.ShopFees.FirstOrDefault(sf => sf.Id == fee.ShopFeeId)
                                : shopDetails.ShopFees.FirstOrDefault(sf => string.Equals(sf.FeeName, fee.Name, StringComparison.OrdinalIgnoreCase));
                            
                            if (shopFee != null)
                            {
                                // Use shop configuration as source of truth for mandatory status
                                isMandatory = shopFee.Mandatory;
                            }
                        }
                        
                        // Check if fee_name is "Service charge" and adjust label based on mandatory flag
                        // For POS orders (platform ID not 9), show "Mandatory Service Charge" or "Discretionary Service Charge"
                        // For platform ID 9, show "Service Charge"
                        if (string.Equals(labelName, "Service charge", StringComparison.OrdinalIgnoreCase))
                        {
                            // Check platform ID - if it's 9, keep "Service Charge", otherwise show mandatory/discretionary
                            if (order.PlatformId != 9 && order.PlatformId2 != 9)
                            {
                                // Keep original "Service Charge" for platform ID not 9
                                labelName = "Service Charge";
                            }
                            else
                            {
                                // For POS orders (platform 9), show mandatory/discretionary
                                labelName = isMandatory ? "Mandatory Service Charge" : "Discretionary Service Charge";
                            }
                        }
                        
                        var type = (fee.FeeType ?? string.Empty).Trim().ToUpperInvariant();
                        string label;
                        
                        if (type == "PERCENTAGE")
                        {
                            // For percentage fees, show the percentage value
                            decimal percentValue = fee.FeeValue;
                            
                            // If FeeValue not set, try to look it up from shop details
                            if (percentValue <= 0 && fee.ShopFeeId > 0 && shopDetails?.ShopFees != null)
                            {
                                var shopFee = shopDetails.ShopFees.FirstOrDefault(sf => sf.Id == fee.ShopFeeId);
                                if (shopFee != null)
                                {
                                    percentValue = shopFee.Fee;
                                }
                            }
                            
                            if (percentValue > 0)
                            {
                                label = $"{labelName}({percentValue:0.##}%)";
                            }
                            else
                            {
                                // Last resort fallback
                                label = $"{labelName}(%)";
                            }
                        }
                        else if (type == "VALUE")
                        {
                            // For value fees, show "(value)" in bracket
                            label = $"{labelName}(value)";
                        }
                        else
                        {
                            // Unknown type, just show the name
                            label = labelName;
                        }
                        
                        receipt.AppendLine($"**{label}:**|**{shopDetails?.Currency ?? "£"} {fee.Amount:F2}**");
                    }
                }
            }
            catch { }

            // Total Fee (if any) - only when there are no individual fee rows
            var hasIndividualFees = order?.OrderShopFees != null && order.OrderShopFees.Count > 0;
            if (!hasIndividualFees && (order?.TotalFee ?? 0m) > 0m)
            {
                receipt.AppendLine($"**SHOP FEE:**|**{shopDetails?.Currency ?? "£"} {order.TotalFee:F2}**");
            }

            var finalTotal = order?.ApiTotal ?? ((order?.Items?.Sum(i => i.Total) ?? 0m) - (order?.DiscountAmount ?? 0m) - (order?.CouponAmount ?? 0m) + (order?.DeliveryCharge ?? 0m));
            receipt.AppendLine($"**TOTAL:**|**{shopDetails?.Currency ?? "£"} {finalTotal:F2}**");

            // Tax summary - prefer tax data from GetOrderById API (order_taxes), otherwise calculate
            List<TaxSummaryRow> orderTaxSummaryRows = null;
            var hasShopTaxProfiles = (shopDetails?.TaxProfiles?.Count ?? 0) > 0;
            
            // Only process tax summary if platformId is 9
            if (order?.PlatformId == 9 || order?.PlatformId2 == 9)
            {
                // Use tax data from API if available (from order_taxes array in GetOrderById response)
                if (order?.TaxSummaryRows != null && order.TaxSummaryRows.Count > 0)
                {
                    orderTaxSummaryRows = order.TaxSummaryRows
                        .Where(r => r != null && (r.TaxAmount > 0m || r.TaxableAmount > 0m))
                        .OrderByDescending(r => r.Rate)
                        .ThenBy(r => r.TaxCode)
                        .ToList();
                }
                
                // Fallback to calculated tax if API tax data is not available
                if (orderTaxSummaryRows == null || orderTaxSummaryRows.Count == 0)
                {
                    orderTaxSummaryRows = BuildOrderTaxSummaryRows(order, shopDetails);
                }
                
                if (hasShopTaxProfiles && orderTaxSummaryRows != null && orderTaxSummaryRows.Count > 0)
                {
                    AppendTaxSummaryBlock(receipt, orderTaxSummaryRows, shopDetails, leftOffset);
                }
            }

            // If paying with CASH, show tendered and balance in same style
            var lastCashGivenIncoming = POS_UI.Services.GlobalDataService.Instance.LastCashGiven;
            var lastCashBalanceIncoming = POS_UI.Services.GlobalDataService.Instance.LastCashBalance;
            var incomingPayMethod = (paymentMethod ?? string.Empty).Trim().ToUpperInvariant();
            if (incomingPayMethod == "CASH" && lastCashGivenIncoming.HasValue)
            {
                receipt.AppendLine($"**PAID AMOUNT:**|**{shopDetails?.Currency ?? "£"} {lastCashGivenIncoming.Value:F2}**");
                var bal = Math.Max(lastCashBalanceIncoming ?? 0m, 0m);
                receipt.AppendLine($"**CASH BALANCE:**|**{shopDetails?.Currency ?? "£"} {bal:F2}**");
            }

            var totalAfterSeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + totalAfterSeparatorLine.PadLeft(totalAfterSeparatorLine.Length + leftOffset) + "**");

            // Payment Method - prioritize paymentMethod parameter, then order properties
            string payMethod = null;
            
            // First, use the paymentMethod parameter if provided (this is set when completing unpaid orders)
            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                payMethod = paymentMethod;
            }
            // If not provided, check order properties
            else
            {
                // For unpaid orders, use PaymentMethod; for paid orders, use PaymentMode
                var paymentStatus = order?.PaymentStatus?.Trim().ToUpperInvariant() ?? "";
                if (paymentStatus == "UNPAID")
                {
                    payMethod = order?.PaymentMethod;
                }
                else
                {
                    // For paid orders, try PaymentMode first, then PaymentMethod as fallback
                    payMethod = order?.PaymentMode ?? order?.PaymentMethod;
                }
            }
            
            if (!string.IsNullOrWhiteSpace(payMethod))
            {
                var splitLines = ParseReceiptSplitPaymentLines(payMethod);
                if (splitLines.Count > 1)
                {
                    AppendSplitPaymentRowsSameStyleAsSingle(receipt, splitLines);
                }
                else
                {
                    payMethod = splitLines.Count == 1 ? splitLines[0] : payMethod.Trim();
                    var payMethodNormalized = payMethod.ToUpperInvariant();

                    if (payMethodNormalized == "MANUALCARD" || payMethodNormalized == "MANUAL CARD")
                    {
                        payMethod = "CARD";
                    }
                    else if (payMethodNormalized == "COD")
                    {
                        payMethod = "Pay on Delivery";
                    }
                    else if (payMethodNormalized == "COT")
                    {
                        payMethod = "Pay on Takeaway";
                    }

                    receipt.AppendLine($"**PAYMENT:**|**{payMethod.ToUpperInvariant()}**");
                }
            }

            var paymentSeparatorLine = "".PadRight(50, '-');
            receipt.AppendLine("**" + paymentSeparatorLine.PadLeft(paymentSeparatorLine.Length + leftOffset) + "**");

            // Cashier near footer, above thank you, to keep it visible but unobtrusive
            var footerCashier = GlobalDataService.Instance.CurrentUser?.FullName;
            if (!string.IsNullOrWhiteSpace(footerCashier))
            {
                receipt.AppendLine($"Cashier: {footerCashier}");
            }

            // Thank you and shop details
            receipt.AppendLine("Thank you for ordering with");

            var settingsService = new SettingsService();
            var (tenantCode, _, _) = settingsService.LoadSettings();
            //Get brand_name from shop details
            var brandName = shopDetails?.DeliveryPlatform?.BrandName;
            /*if (!string.IsNullOrEmpty(brandName))
            {
                receipt.AppendLine($"**{brandName.ToUpper()}**");
            }*/

            if (!string.IsNullOrEmpty(brandName) && !string.IsNullOrEmpty(shopDetails?.Name))
            {
                var platformShopText = $"{brandName} - {shopDetails.Name}";
                int maxLineLength = 32; // depends on printer paper width (e.g. 32 chars for 58mm, 42 for 80mm)

                var words = platformShopText.Split(' ');
                var currentLine = new StringBuilder();
                var lines = new List<string>();

                foreach (var word in words)
                {
                    if (currentLine.Length + word.Length + 1 > maxLineLength)
                    {
                        lines.Add(currentLine.ToString().Trim());
                        currentLine.Clear();
                    }
                    currentLine.Append(word + " ");
                }

                if (currentLine.Length > 0)
                    lines.Add(currentLine.ToString().Trim());

                // Apply bold to each line individually to ensure consistent formatting
                foreach (var line in lines)
                {
                    receipt.AppendLine($"**{line}**");
                }
            }

            if (!string.IsNullOrEmpty(shopDetails?.Address))
            {
                var addressText = shopDetails.Address;
                var maxLineLength = 32;
                if (addressText.Length <= maxLineLength)
                {
                    receipt.AppendLine($"**{addressText}**");
                }
                else
                {
                    var words = addressText.Split(' ');
                    var currentLine = "";
                    foreach (var word in words)
                    {
                        if ((currentLine + " " + word).Length <= maxLineLength)
                        {
                            currentLine += (currentLine.Length > 0 ? " " : "") + word;
                        }
                        else
                        {
                            if (currentLine.Length > 0)
                            {
                                receipt.AppendLine($"**{currentLine}**");
                                currentLine = word;
                            }
                            else
                            {
                                receipt.AppendLine($"**{word}**");
                            }
                        }
                    }
                    if (currentLine.Length > 0)
                    {
                        receipt.AppendLine($"**{currentLine}**");
                    }
                }
            }

            if (!string.IsNullOrEmpty(shopDetails?.ContactNo))
            {
                receipt.AppendLine($"**{shopDetails.ContactNo}**");
            }

            var taxRegNo = shopDetails?.TaxRegNo?.Trim();
            if (!string.IsNullOrEmpty(taxRegNo))
            {
                // Single line, no | — PrintToPrinterAsync draws this centered (tax reg branch)
                receipt.AppendLine($"**TAX REG NO: {taxRegNo}**");
            }
            // Powered by Delivergate (centered and italic)
            receipt.AppendLine(":ITALIC:Powered by Delivergate");

            return receipt.ToString();
        }

        private static List<TaxSummaryRow> BuildOrderTaxSummaryRows(OrderModel order, ShopModel shopDetails)
        {
            var summary = new Dictionary<string, TaxSummaryRow>(StringComparer.OrdinalIgnoreCase);
            if (order == null) return new List<TaxSummaryRow>();
            var taxInclusive = shopDetails?.TaxInclusive ?? true;

            void Accumulate(string code, decimal rate, decimal amount, decimal taxable, bool allowEstimate)
            {
                var normalizedTaxAmount = Math.Round(Math.Max(0m, amount), 2, MidpointRounding.AwayFromZero);
                var normalizedTaxable = Math.Round(Math.Max(0m, taxable), 2, MidpointRounding.AwayFromZero);
                if (normalizedTaxable <= 0m && allowEstimate && normalizedTaxAmount > 0m && rate > 0m)
                {
                    normalizedTaxable = EstimateTaxableFromTaxAmount(normalizedTaxAmount, rate, taxInclusive);
                }

                if (normalizedTaxAmount <= 0m && normalizedTaxable <= 0m)
                {
                    return;
                }

                var codeKey = !string.IsNullOrWhiteSpace(code)
                    ? code
                    : (rate > 0m ? $"TAX_{rate:0.##}" : "ZERO");

                if (!summary.TryGetValue(codeKey, out var row))
                {
                    row = new TaxSummaryRow
                    {
                        TaxCode = codeKey,
                        Rate = rate
                    };
                    summary[codeKey] = row;
                }

                row.TaxAmount = Math.Round(row.TaxAmount + normalizedTaxAmount, 2, MidpointRounding.AwayFromZero);
                row.TaxableAmount = Math.Round(row.TaxableAmount + normalizedTaxable, 2, MidpointRounding.AwayFromZero);
                if (row.Rate <= 0m && rate > 0m)
                {
                    row.Rate = rate;
                }
            }

            if (order.Items != null)
            {
                foreach (var item in order.Items)
                {
                    if (item?.TaxDetails == null) continue;
                    foreach (var detail in item.TaxDetails)
                    {
                        if (detail == null) continue;
                        
                        var amount = detail.Amount;
                        var taxable = detail.TaxableAmount;
                        
                        // Component details (modifiers) are per-unit, multiply by quantity
                        // Primary details (base product) are already line totals
                        if (detail.IsComponentDetail && item.Quantity > 1)
                        {
                            amount *= item.Quantity;
                            taxable *= item.Quantity;
                        }
                        
                        var needsEstimate = taxable <= 0m;
                        Accumulate(detail.TaxCode, detail.Rate, amount, taxable, needsEstimate);
                    }
                }
            }

            if (order.OrderShopFees != null)
            {
                foreach (var fee in order.OrderShopFees)
                {
                    if (fee == null) continue;
                    var feeTaxable = fee.Amount;
                    var allowEstimate = feeTaxable <= 0m;
                    Accumulate(fee.TaxCode, fee.TaxRate, fee.TaxAmount, feeTaxable, allowEstimate);
                }
            }

            if (order.DeliveryTaxDetail != null)
            {
                var delivery = order.DeliveryTaxDetail;
                var deliveryTaxable = delivery.TaxableAmount;
                var needsEstimate = deliveryTaxable <= 0m;
                Accumulate(delivery.TaxCode, delivery.Rate, delivery.Amount, deliveryTaxable, needsEstimate);
            }

            // Distribute Order Discount and Voucher across summary rows
            // Note: Use VoucherDiscount instead of CouponAmount to match KitchenOrderDetailsDialogViewModel logic
            var totalDeduction = (order.DiscountAmount) + (order.VoucherDiscount);
            if (totalDeduction > 0)
            {
                var totalTaxable = summary.Values.Sum(r => r.TaxableAmount);
                if (totalTaxable > 0)
                {
                    decimal remainingDeduction = totalDeduction;
                    // Create a list to iterate safely
                    var rows = summary.Values.OrderByDescending(r => r.TaxableAmount).ToList();
                    
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var row = rows[i];
                        if (row.TaxableAmount <= 0) continue;

                        decimal share = 0m;
                        // For the last row (or effectively last with value), apply remaining to avoid rounding gaps
                        // Note: proportional distribution logic
                        if (remainingDeduction <= 0) break;
                        
                        // Calculate share based on proportion of this row to total taxable
                        // This assumes the discount applies to all taxable items equally
                        share = Math.Round(totalDeduction * (row.TaxableAmount / totalTaxable), 2, MidpointRounding.AwayFromZero);
                        
                        // Adjust for last item rounding
                        if (i == rows.Count - 1)
                        {
                            share = remainingDeduction;
                        }

                        // Ensure we don't deduct more than available on the row (unless negative tax is allowed, but let's cap at 0 for now)
                        // Also ensure we don't deduct more than remaining
                        var actualDeduction = Math.Min(row.TaxableAmount, share);
                        actualDeduction = Math.Min(actualDeduction, remainingDeduction);
                        
                        row.TaxableAmount = Math.Max(0m, row.TaxableAmount - actualDeduction);
                        remainingDeduction -= actualDeduction;
                    }
                    
                    // If any deduction remains (due to capping), force apply to the largest row
                    if (remainingDeduction > 0)
                    {
                         var largestRow = rows.FirstOrDefault();
                         if (largestRow != null)
                         {
                             largestRow.TaxableAmount = Math.Max(0m, largestRow.TaxableAmount - remainingDeduction);
                         }
                    }
                }
            }

            // Recalculate tax amounts based on updated taxable after discount/coupon distribution
            foreach (var row in summary.Values)
            {
                if (row.Rate <= 0m || row.TaxableAmount <= 0m)
                {
                    row.TaxAmount = 0m;
                    continue;
                }

                if (taxInclusive)
                {
                    // Tax inclusive: taxable contains tax; extract the tax portion
                    // Multiply first to maintain precision, then divide
                    row.TaxAmount = Math.Round((row.TaxableAmount * row.Rate) / (100m + row.Rate), 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    // Tax exclusive: tax is applied on the taxable base
                    // Multiply first to maintain precision, then divide
                    row.TaxAmount = Math.Round((row.TaxableAmount * row.Rate) / 100m, 2, MidpointRounding.AwayFromZero);
                }
            }

            return summary.Values
                .Where(r => r != null && (r.TaxAmount > 0m || r.TaxableAmount > 0m))
                .OrderByDescending(r => r.Rate)
                .ThenBy(r => r.TaxCode)
                .ToList();
        }

        private static void AppendTaxSummaryBlock(StringBuilder receipt, IList<TaxSummaryRow> rows, ShopModel shopDetails, int leftOffset)
        {
            if (receipt == null || rows == null || rows.Count == 0) return;

            var currency = shopDetails?.Currency ?? "£";
            var separator = "".PadRight(50, '-');

            // Separator and title
            receipt.AppendLine("**" + separator.PadLeft(separator.Length + leftOffset) + "**");
            receipt.AppendLine("**TAX SUMMARY**");
            receipt.AppendLine("**" + separator.PadLeft(separator.Length + leftOffset) + "**");

            foreach (var row in rows)
            {
                if (row == null) continue;
                var rateText = row.Rate > 0 ? $"{row.Rate:0.##}%" : "0%";
                var taxCode = string.IsNullOrWhiteSpace(row.TaxCode) ? "TAX" : row.TaxCode;
                var taxLabel = $"{taxCode} ({rateText})";
                var taxValue = $"{currency} {row.TaxableAmount:F2} / {currency} {row.TaxAmount:F2}";
                receipt.AppendLine($"**{taxLabel}:**|**{taxValue}**");
            }

            var totalTax = rows.Where(r => r != null).Sum(r => r.TaxAmount);
            receipt.AppendLine($"**TOTAL TAX:**|**{currency} {totalTax:F2}**");
            receipt.AppendLine("**" + separator.PadLeft(separator.Length + leftOffset) + "**");
        }

        private static decimal EstimateTaxableFromTaxAmount(decimal taxAmount, decimal rate, bool taxInclusive)
        {
            if (rate <= 0m) return 0m;
            if (taxInclusive)
            {
                return Math.Round(taxAmount * (100m + rate) / rate, 2, MidpointRounding.AwayFromZero);
            }
            return Math.Round(taxAmount * (100m / rate), 2, MidpointRounding.AwayFromZero);
        }

        private string GenerateKitchenReceiptContent(CartService cartService)
        {
            var receipt = new StringBuilder();
           /* var shopDetails = GlobalDataService.Instance.ShopDetails;
            
            // Shop logo marker (if logo exists) - will be rendered by PrintToPrinterAsync
            if (!string.IsNullOrWhiteSpace(shopDetails?.ShopLogo))
            {
                receipt.AppendLine($":LOGO:{shopDetails.ShopLogo}");
            }*/
            
            receipt.AppendLine("**KITCHEN RECEIPT**");
            receipt.AppendLine(new string('=', 50));
            
            // Required minimal fields
            var orderId = cartService.DisplayOrderId;
            var orderType = (cartService.OrderType ?? "Take Away").ToUpper();
            var printedAt = DateTime.Now;
            var shopDetails = GlobalDataService.Instance.ShopDetails;
            var platformName = cartService?.DeliveryPlatformName;
            // Fallback to shop platform name if cart service platform name is empty
            if (string.IsNullOrEmpty(platformName))
            {
                platformName = shopDetails?.DeliveryPlatform?.Name;
            }
            AppendPlatformAndOrderHeader(receipt, platformName, orderId);
            // Print order type as a bold, standalone header to trigger large centered styling
            receipt.AppendLine($"**{orderType}**");

            // Dine In: include table NAME prominently BEFORE printed time (no numeric fallback)
            if (string.Equals(orderType, "DINE IN", StringComparison.OrdinalIgnoreCase))
            {
                var tableLabel = cartService.TableName;
                if (!string.IsNullOrWhiteSpace(tableLabel))
                {
                    // Use label that triggers big, centered formatting in renderer
                    receipt.AppendLine($"**Table: {tableLabel}**");
                }
            }

            // Printed time (after table info if any)
            receipt.AppendLine($"Printed: {FormatDateWithTime(printedAt)}");
            // For non-dine-in orders, include pickup/delivery time after printed time when available
            if (!string.Equals(orderType, "DINE IN", StringComparison.OrdinalIgnoreCase) && cartService.PickupTime.HasValue)
            {
                receipt.AppendLine($"Pickup: {FormatDateWithTime(cartService.PickupTime.Value)}");
            }

            // Order Note (if any) before listing items
            if (!string.IsNullOrWhiteSpace(cartService?.Note))
            {
                var orderNoteText = (cartService.Note ?? string.Empty).Trim();
                receipt.AppendLine("**" + new string('-', 50) + "**");
                receipt.AppendLine($"**Order Note:** {orderNoteText}");
            }

            // Double-line separator before items (to match end)
            receipt.AppendLine(new string('=', 50));

            var itemsList = cartService.OrderItems?.ToList() ?? new List<POS_UI.Models.OrderItem>();
            var starters = itemsList.Where(IsStarterCategory).ToList();
            var others = itemsList.Where(i => !IsStarterCategory(i)).ToList();

            bool wroteAny = false;
            if (starters.Count > 0)
            {
                receipt.AppendLine("**STARTERS**");
                for (int i = 0; i < starters.Count; i++)
                {
                    var item = starters[i];
                    var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                    AppendKitchenItemLine(receipt, itemLine);
                var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                    ? item.ExternalModifierDetailsForDisplay
                    : item.ModifierDetailsForDisplay;
                if (modifiersToPrint != null && modifiersToPrint.Any())
                {
                    foreach (var modifier in modifiersToPrint)
                    {
                        var text = modifier?.DisplayText ?? modifier?.ModifierName;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            receipt.AppendLine($"  {text}");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(item?.Note))
                {
                    receipt.AppendLine($"  Note: {item.Note}");
                    }
                    if (i < starters.Count - 1)
                    {
                        receipt.AppendLine(new string('-', 50));
                    }
                }
                wroteAny = true;
                if (others.Count > 0)
                {
                    receipt.AppendLine(new string('=', 50));
                    receipt.AppendLine("**ITEMS:**");
                }
            }
            else
            {
                // No starters; label the single section as ITEMS
                receipt.AppendLine("**ITEMS:**");
            }

            for (int i = 0; i < others.Count; i++)
            {
                var item = others[i];
                var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                AppendKitchenItemLine(receipt, itemLine);
                var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                    ? item.ExternalModifierDetailsForDisplay
                    : item.ModifierDetailsForDisplay;
                if (modifiersToPrint != null && modifiersToPrint.Any())
                {
                    foreach (var modifier in modifiersToPrint)
                    {
                        var text = modifier?.DisplayText ?? modifier?.ModifierName;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            receipt.AppendLine($"  {text}");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(item?.Note))
                {
                    receipt.AppendLine($"  Note: {item.Note}");
                }
                if (i < others.Count - 1)
                {
                    receipt.AppendLine(new string('-', 50));
                }
            }

            receipt.AppendLine(new string('=', 50));
            
            // Powered by Delivergate (centered and italic)
            receipt.AppendLine(":ITALIC:Powered by Delivergate");
            
            return receipt.ToString();
        }

        private string GenerateKitchenReceiptContentForItems(CartService cartService, IEnumerable<OrderItem> items, string printerGroupName = null)
        {
            var receipt = new StringBuilder();
            /*var shopDetails = GlobalDataService.Instance.ShopDetails;

            // Shop logo marker (if logo exists) - will be rendered by PrintToPrinterAsync
            if (!string.IsNullOrWhiteSpace(shopDetails?.ShopLogo))
            {
                receipt.AppendLine($":LOGO:{shopDetails.ShopLogo}");
            }*/

            var title = !string.IsNullOrWhiteSpace(printerGroupName) ? printerGroupName : "KITCHEN RECEIPT";
            receipt.AppendLine($":KITCHEN RECEIPT:{title}");
            receipt.AppendLine(new string('=', 50));

            var orderId = cartService.DisplayOrderId;
            var orderType = (cartService.OrderType ?? "Take Away").ToUpper();
            var printedAt = DateTime.Now;
            var shopDetails = GlobalDataService.Instance.ShopDetails;
            var platformName = cartService?.DeliveryPlatformName;
            // Fallback to shop platform name if cart service platform name is empty
            if (string.IsNullOrEmpty(platformName))
            {
                platformName = shopDetails?.DeliveryPlatform?.Name;
            }
            AppendPlatformAndOrderHeader(receipt, platformName, orderId);
            receipt.AppendLine($"**{orderType}**");

            if (string.Equals(orderType, "DINE IN", StringComparison.OrdinalIgnoreCase))
            {
                var tableLabel = cartService.TableName;
                if (!string.IsNullOrWhiteSpace(tableLabel))
                {
                    receipt.AppendLine($"**Table: {tableLabel}**");
                }
            }

            receipt.AppendLine($"Printed: {FormatDateWithTime(printedAt)}");

            if (!string.IsNullOrWhiteSpace(cartService?.Note))
            {
                var orderNoteText = (cartService.Note ?? string.Empty).Trim();
                receipt.AppendLine("**" + new string('-', 50) + "**");
                receipt.AppendLine($"**Order Note:** {orderNoteText}");
            }

            receipt.AppendLine(new string('=', 50));

            var itemsList = items.ToList();
            var starters = itemsList.Where(IsStarterCategory).ToList();
            var others = itemsList.Where(i => !IsStarterCategory(i)).ToList();

            if (starters.Count > 0)
            {
                receipt.AppendLine("**STARTERS**");
                for (int i = 0; i < starters.Count; i++)
                {
                    var item = starters[i];
                    var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                    AppendKitchenItemLine(receipt, itemLine);
                    var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                        ? item.ExternalModifierDetailsForDisplay
                        : item.ModifierDetailsForDisplay;
                    if (modifiersToPrint != null && modifiersToPrint.Any())
                    {
                        foreach (var modifier in modifiersToPrint)
                        {
                            var text = modifier?.DisplayText ?? modifier?.ModifierName;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                receipt.AppendLine($"  {text}");
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(item?.Note))
                    {
                        receipt.AppendLine($"  Note: {item.Note}");
                    }
                    if (i < starters.Count - 1)
                    {
                        receipt.AppendLine(new string('-', 50));
                    }
                }
                if (others.Count > 0)
                {
                    receipt.AppendLine(new string('=', 50));
                    receipt.AppendLine("**ITEMS:**");
                }
            }
            else
            {
                // No starters; label the single section as ITEMS
                receipt.AppendLine("**ITEMS:**");
            }

            for (int i = 0; i < others.Count; i++)
            {
                var item = others[i];
                var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                AppendKitchenItemLine(receipt, itemLine);
                var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                    ? item.ExternalModifierDetailsForDisplay
                    : item.ModifierDetailsForDisplay;
                if (modifiersToPrint != null && modifiersToPrint.Any())
                {
                    foreach (var modifier in modifiersToPrint)
                    {
                        var text = modifier?.DisplayText ?? modifier?.ModifierName;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            receipt.AppendLine($"  {text}");
                        }
                    }
                }
                if (!string.IsNullOrWhiteSpace(item?.Note))
                {
                    receipt.AppendLine($"  Note: {item.Note}");
                }
                if (i < others.Count - 1)
                {
                    receipt.AppendLine(new string('-', 50));
                }
            }

            receipt.AppendLine(new string('=', 50));
            
            // Powered by Delivergate (centered and italic)
            receipt.AppendLine(":ITALIC:Powered by Delivergate");
            
            return receipt.ToString();
        }

        private string GenerateKitchenReceiptContentFromOrder(OrderModel order, string printerGroupName = null)
        {
            var receipt = new StringBuilder();
            /*var shopDetails = GlobalDataService.Instance.ShopDetails;
            
            // Shop logo marker (if logo exists) - will be rendered by PrintToPrinterAsync
            if (!string.IsNullOrWhiteSpace(shopDetails?.ShopLogo))
            {
                receipt.AppendLine($":LOGO:{shopDetails.ShopLogo}");
            }*/
            
            var title = !string.IsNullOrWhiteSpace(printerGroupName) ? printerGroupName : "KITCHEN RECEIPT";
            receipt.AppendLine($":KITCHEN RECEIPT:{title}");
            receipt.AppendLine(new string('=', 50));

            var orderId = order?.DisplayOrderId ?? order?.OrderNumber ?? (order?.ApiId > 0 ? order.ApiId.ToString() : "N/A");
            var orderTypeText = (order?.OrderType == Models.OrderType.DineIn ? "DINE IN" : order?.OrderType == Models.OrderType.Delivery ? "DELIVERY" : "TAKE AWAY");
            // Table order (Platform "Table order" or platform id 8): print the order's TableOrderMethod as order type
            var isTableOrder = string.Equals(order?.Platform, "Table order", StringComparison.OrdinalIgnoreCase) || (order != null && (order.PlatformId == 8 || order.PlatformId2 == 8));
            if (isTableOrder && !string.IsNullOrWhiteSpace(order?.TableOrderMethod))
            {
                orderTypeText = order.TableOrderMethod.Trim().ToUpperInvariant().Replace("-", " ").Replace("TAKEAWAY", "TAKE AWAY");
            }
            else if (isTableOrder)
            {
                orderTypeText = "DINE IN"; // fallback when TableOrderMethod not set
            }
            var printedAt = DateTime.Now;
            var platformName = order?.DeliveryPlatfornName;
            AppendPlatformAndOrderHeader(receipt, platformName, orderId);
            receipt.AppendLine($"**{orderTypeText}**");

            // Table order: print selected table name (same resolution as main receipt)
            if (isTableOrder)
            {
                var resolvedTableName = !string.IsNullOrWhiteSpace(order?.TableName)
                    ? order.TableName
                    : POS_UI.Services.GlobalDataService.Instance?.CurrentOrderForEdit?.TableName;
                if (!string.IsNullOrWhiteSpace(resolvedTableName))
                {
                    receipt.AppendLine($"**Table: {resolvedTableName}**");
                }
            }

            receipt.AppendLine($"Printed: {FormatDateWithTime(printedAt)}");
            if (!string.Equals(orderTypeText, "DINE IN", StringComparison.OrdinalIgnoreCase) && !string.Equals(order?.Platform, "Table order", StringComparison.OrdinalIgnoreCase) && !string.Equals(order?.TableOrderMethod, "Dine-in", StringComparison.OrdinalIgnoreCase))
            {
                var pickupSrc = order?.ScheduledTime ?? order?.DeliveryDateTime;
                if (pickupSrc.HasValue)
                {
                    DateTime pickupTime;
                    if (order?.IsFromPhpApi == true)
                    {
                        pickupTime = pickupSrc.Value;
                    }
                    else
                    {
                        try
                        {
                            if (pickupSrc.Value.Kind == DateTimeKind.Local)
                            {
                                pickupTime = pickupSrc.Value.ToUniversalTime();
                            }
                            else if (pickupSrc.Value.Kind == DateTimeKind.Unspecified && order?.DeliveryDateTime.HasValue == true)
                            {
                                pickupTime = DateTime.SpecifyKind(pickupSrc.Value, DateTimeKind.Local).ToUniversalTime();
                            }
                            else
                            {
                                pickupTime = pickupSrc.Value;
                            }
                        }
                        catch { pickupTime = pickupSrc.Value; }
                    }
                    receipt.AppendLine($"Pickup: {FormatDateWithTime(pickupTime)}");
                }
            }

            // Order Note (if any) before listing items
            if (!string.IsNullOrWhiteSpace(order?.OrderNotes))
            {
                var orderNoteText = (order.OrderNotes ?? string.Empty).Trim();
                receipt.AppendLine("**" + new string('-', 50) + "**");
                receipt.AppendLine($"**Order Note:** {orderNoteText}");
            }

            receipt.AppendLine(new string('=', 50));

            if (order?.Items != null)
            {
                var starters = order.Items.Where(IsStarterCategory).ToList();
                var others = order.Items.Where(i => !IsStarterCategory(i)).ToList();

                if (starters.Count > 0)
                {
                    receipt.AppendLine("**STARTERS**");
                    for (int i = 0; i < starters.Count; i++)
                    {
                        var item = starters[i];
                        var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                        AppendKitchenItemLine(receipt, itemLine);
                    var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                        ? item.ExternalModifierDetailsForDisplay
                        : item.ModifierDetailsForDisplay;
                    if (modifiersToPrint != null && modifiersToPrint.Any())
                    {
                        foreach (var modifier in modifiersToPrint)
                        {
                            var text = modifier?.DisplayText ?? modifier?.ModifierName;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                receipt.AppendLine($"  {text}");
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(item?.Note))
                    {
                        receipt.AppendLine($"  Note: {item.Note}");
                    }
                        if (i < starters.Count - 1)
                        {
                            receipt.AppendLine(new string('-', 50));
                        }
                    }
                    if (others.Count > 0)
                    {
                        receipt.AppendLine(new string('=', 50));
                        receipt.AppendLine("**ITEMS:**");
                    }
                }
                else
                {
                    // No starters; label the single section as ITEMS
                    receipt.AppendLine("**ITEMS:**");
                }

                for (int i = 0; i < others.Count; i++)
                {
                    var item = others[i];
                    var itemLine = $"[{item.Quantity}x] {item.DisplayName}";
                    AppendKitchenItemLine(receipt, itemLine);
                    var modifiersToPrint = (item.ExternalModifierDetailsForDisplay != null && item.ExternalModifierDetailsForDisplay.Count > 0)
                        ? item.ExternalModifierDetailsForDisplay
                        : item.ModifierDetailsForDisplay;
                    if (modifiersToPrint != null && modifiersToPrint.Any())
                    {
                        foreach (var modifier in modifiersToPrint)
                        {
                            var text = modifier?.DisplayText ?? modifier?.ModifierName;
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                receipt.AppendLine($"  {text}");
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(item?.Note))
                    {
                        receipt.AppendLine($"  Note: {item.Note}");
                    }
                    if (i < others.Count - 1)
                    {
                        receipt.AppendLine(new string('-', 50));
                    }
                }
            }

            receipt.AppendLine(new string('=', 50));
            
            // Powered by Delivergate (centered and italic)
            receipt.AppendLine(":ITALIC:Powered by Delivergate");
            
            return receipt.ToString();
        }

        private static void AppendItemLineWithPrice(StringBuilder receipt, string itemLine, string priceLine)
        {
            if (receipt == null)
            {
                return;
            }

            // For 80mm receipt printer, typical width is 48 characters
            // Price line is usually around 12 characters (e.g., "Rs. 450.00")
            // Reserve space for price and separator, allow ~35 characters for item name
            const int maxItemNameLength = 35;
            const int priceLength = 12; // Approximate price width
            const int maxFirstLineLength = maxItemNameLength - priceLength - 3; // Reserve space for price on first line

            // Check if item name fits on one line with price
            if (itemLine.Length <= maxItemNameLength)
            {
                // Fits on one line - use standard format
                receipt.AppendLine($"**{itemLine}**|**{priceLine}**");
                return;
            }

            // Item name is too long - need to wrap
            // Always try to put price on first line (right-aligned)
            var words = itemLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var firstLine = string.Empty;
            var remainingWords = new List<string>();

            // Try to fit as many words as possible on first line, leaving room for price
            foreach (var word in words)
            {
                var candidate = firstLine.Length == 0 ? word : firstLine + " " + word;
                // Reserve space for price on first line
                if (candidate.Length <= maxFirstLineLength)
                {
                    firstLine = candidate;
                }
                else
                {
                    remainingWords.Add(word);
                }
            }

            // Always put price on first line (right-aligned using | separator)
            if (firstLine.Length > 0)
            {
                receipt.AppendLine($"**{firstLine}**|**{priceLine}**");
            }
            else
            {
                // Even first word is too long - put full item name and price on separate lines
                // This is an edge case, but we'll handle it
                receipt.AppendLine($"**{itemLine}**");
                receipt.AppendLine($"**{priceLine}**");
                return;
            }

            // Add remaining words as wrapped lines (left-aligned, no price)
            if (remainingWords.Count > 0)
            {
                var currentLine = string.Empty;
                foreach (var word in remainingWords)
                {
                    var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
                    if (candidate.Length <= maxItemNameLength)
                    {
                        currentLine = candidate;
                    }
                    else
                    {
                        if (currentLine.Length > 0)
                        {
                            receipt.AppendLine($"**{currentLine}**");
                        }
                        currentLine = word;
                    }
                }
                if (currentLine.Length > 0)
                {
                    receipt.AppendLine($"**{currentLine}**");
                }
            }
        }

        private static void AppendKitchenItemLine(StringBuilder receipt, string itemLine)
        {
            if (receipt == null)
            {
                return;
            }

            // For 80mm receipt printer, typical width is 48 characters
            // Kitchen receipts don't have prices, so we can use more space for item names
            const int maxItemNameLength = 40; // Slightly more than main receipt since no price

            // Check if item name fits on one line
            if (itemLine.Length <= maxItemNameLength)
            {
                // Fits on one line - use standard format
                receipt.AppendLine($"**{itemLine}**");
                return;
            }

            // Item name is too long - need to wrap
            var words = itemLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var firstLine = string.Empty;
            var remainingWords = new List<string>();

            // Try to fit as many words as possible on first line
            foreach (var word in words)
            {
                var candidate = firstLine.Length == 0 ? word : firstLine + " " + word;
                if (candidate.Length <= maxItemNameLength)
                {
                    firstLine = candidate;
                }
                else
                {
                    remainingWords.Add(word);
                }
            }

            // Print first line
            if (firstLine.Length > 0)
            {
                receipt.AppendLine($"**{firstLine}**");
            }
            else
            {
                // Even first word is too long - print full item name
                receipt.AppendLine($"**{itemLine}**");
                return;
            }

            // Add remaining words as wrapped lines
            if (remainingWords.Count > 0)
            {
                var currentLine = string.Empty;
                foreach (var word in remainingWords)
                {
                    var candidate = currentLine.Length == 0 ? word : currentLine + " " + word;
                    if (candidate.Length <= maxItemNameLength)
                    {
                        currentLine = candidate;
                    }
                    else
                    {
                        if (currentLine.Length > 0)
                        {
                            receipt.AppendLine($"**{currentLine}**");
                        }
                        currentLine = word;
                    }
                }
                if (currentLine.Length > 0)
                {
                    receipt.AppendLine($"**{currentLine}**");
                }
            }
        }

        private static void AppendPlatformAndOrderHeader(StringBuilder receipt, string platformName, string orderId)
        {
            if (receipt == null)
            {
                return;
            }

            var normalizedOrderId = string.IsNullOrWhiteSpace(orderId) ? "N/A" : orderId;

            if (!string.IsNullOrWhiteSpace(platformName))
            {
                const int maxLeftTextLength = 11;

                if (platformName.Length <= maxLeftTextLength)
                {
                    receipt.AppendLine($"**{platformName}**|**{normalizedOrderId}**");
                    return;
                }

                var words = platformName.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var firstLine = string.Empty;
                var wordIndex = 0;

                for (int i = 0; i < words.Length; i++)
                {
                    var candidate = firstLine.Length == 0 ? words[i] : firstLine + " " + words[i];
                    if (candidate.Length <= maxLeftTextLength)
                    {
                        firstLine = candidate;
                        wordIndex = i + 1;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(firstLine))
                {
                    receipt.AppendLine($"**{firstLine}**|**{normalizedOrderId}**");
                }
                else
                {
                    receipt.AppendLine($"**{normalizedOrderId}**");
                    wordIndex = 0;
                }

                var currentLine = string.Empty;
                for (int i = wordIndex; i < words.Length; i++)
                {
                    var word = words[i];

                    if (currentLine.Length == 0)
                    {
                        currentLine = word;
                    }
                    else
                    {
                        var candidate = currentLine + " " + word;
                        if (candidate.Length <= maxLeftTextLength)
                        {
                            currentLine = candidate;
                        }
                        else
                        {
                            receipt.AppendLine($"**{currentLine}**");
                            currentLine = word;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(currentLine))
                {
                    receipt.AppendLine($"**{currentLine}**");
                }
            }
            else
            {
                receipt.AppendLine($"**{normalizedOrderId}**");
            }
        }

        private static bool IsStarterCategory(OrderItem item)
        {
            try
            {
                // Check across multiple plausible text sources: category, product name, item name, display name
                var candidates = new List<string>
                {
                    item?.Product?.Category,
                    item?.CategoryName,
                    item?.Product?.ItemName,
                    item?.Name,
                    item?.DisplayName
                };
                foreach (var text in candidates)
                {
                    if (string.IsNullOrWhiteSpace(text)) continue;
                    var t = text.Trim();
                    if (t.IndexOf("starter", StringComparison.OrdinalIgnoreCase) >= 0) // matches starter/starters
                    {
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

        private async Task PrintToPrinterAsync(string printerName, string content)
        {
            await Task.Run(() =>
            {
                try
                {
                    var printDocument = new PrintDocument();
                    printDocument.PrinterSettings.PrinterName = printerName;
                    
                    // Variables to track printing state across multiple pages
                    var lines = content.Split('\n');
                    int currentLineIndex = 0;
                    var inTable11 = false; // Flag to track if we're in a table that should use font size 11 - moved outside to persist across pages
                    
                    printDocument.PrintPage += (sender, e) =>
                    {
                        var inDeliveryAddressBlock = false;
                        var inKitchenReceipt = false;
                        var inPlatformNameSection = false; // Track if we're in platform name section
                        var inPlatformShopTextSection = false; // Track if we're in footer platform shop text section
                        var justPrintedLogo = false; // Track if we just printed the logo (so next line is platform name)
                        var inItemSection = false; // Track if we're in an item section (to only apply wrapped item styling)
                        var y = 5f; // Start closer to top for 80mm printer
                        var leftMargin = 5f; // Left margin for 80mm printer
                        var rightMargin = 5f; // Equal right margin for 80mm printer
                        var pageWidth = e.PageBounds.Width - leftMargin - rightMargin;
                        var centerX = e.PageBounds.Width / 2; // Center point of page
                        var pageHeight = e.PageBounds.Height;
                        var bottomMargin = 20f; // Bottom margin to avoid cutting off content
                        var maxY = pageHeight - bottomMargin; // Maximum Y position before page break
                        
                        // Compute printable center (between left/right margins)
                        var printableCenterX = leftMargin + (pageWidth / 2);
                        var inZReport = false;
                        
                        // Restore table state if we're starting a new page in the middle of a table
                        // Check if we're still in a table by counting TABLE11 and ENDTABLE11 markers before currentLineIndex
                        if (currentLineIndex > 0)
                        {
                            int table11Count = 0;
                            int endTable11Count = 0;
                            for (int j = 0; j < currentLineIndex; j++)
                            {
                                var checkLine = lines[j].TrimEnd('\r');
                                if (checkLine.StartsWith(":TABLE11:"))
                                    table11Count++;
                                if (checkLine.StartsWith(":ENDTABLE11:"))
                                    endTable11Count++;
                            }
                            inTable11 = table11Count > endTable11Count;
                        }
                        
                        // Process lines starting from currentLineIndex
                        for (int i = currentLineIndex; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            var trimmedLine = line.TrimEnd('\r');
                            
                            // Check if we need a page break before processing this line
                            if (y > maxY)
                            {
                                e.HasMorePages = true;
                                currentLineIndex = i; // Save current position for next page
                                return; // Exit to print next page
                            }
                            
                            if (string.IsNullOrEmpty(trimmedLine))
                            {
                                inDeliveryAddressBlock = false;
                                inItemSection = false; // Clear item section flag on empty line (new item or section)
                                y += 12f; // Empty line spacing
                                
                                // Check for page break after incrementing y
                                if (y > maxY)
                                {
                                    e.HasMorePages = true;
                                    currentLineIndex = i + 1;
                                    return;
                                }
                                continue;
                            }
                            
                            // Clear item section flag on separator lines (kitchen receipts use = and -)
                            if ((trimmedLine.All(ch => ch == '-') || trimmedLine.All(ch => ch == '=')) && trimmedLine.Length >= 10)
                            {
                                inItemSection = false;
                            }
                            
                            // Clear item section flag on note lines in kitchen receipts
                            if (inKitchenReceipt && trimmedLine.StartsWith("  Note:"))
                            {
                                inItemSection = false;
                            }
                            
                            // Handle table font size flag
                            if (trimmedLine.StartsWith(":TABLE11:"))
                            {
                                inTable11 = true;
                                continue; // Skip this line, don't print it
                            }
                            
                            if (trimmedLine.StartsWith(":ENDTABLE11:"))
                            {
                                inTable11 = false;
                                continue; // Skip this line, don't print it
                            }
                            
                            // Handle shop logo marker
                            if (trimmedLine.StartsWith(":LOGO:"))
                            {
                                try
                                {
                                    var logoUrl = trimmedLine.Substring(6); // Remove ":LOGO:" prefix
                                    if (!string.IsNullOrWhiteSpace(logoUrl))
                                    {
                                        // Download and draw logo
                                        using (var httpClient = new HttpClient())
                                        {
                                            httpClient.Timeout = TimeSpan.FromSeconds(5); // 5 second timeout
                                            var imageBytes = httpClient.GetByteArrayAsync(logoUrl).GetAwaiter().GetResult();
                                            if (imageBytes != null && imageBytes.Length > 0)
                                            {
                                                using (var ms = new MemoryStream(imageBytes))
                                                {
                                                    using (var logoImage = Image.FromStream(ms))
                                                    {
                                                        // Calculate logo size (max width 60mm for 80mm printer, maintain aspect ratio)
                                                        float maxLogoWidth = pageWidth * 0.75f; // 75% of page width
                                                        float maxLogoHeight = 60f; // Max height in pixels
                                                        float logoWidth = logoImage.Width;
                                                        float logoHeight = logoImage.Height;
                                                        
                                                        // Scale down if needed while maintaining aspect ratio
                                                        float scale = Math.Min(1f, Math.Min(maxLogoWidth / logoWidth, maxLogoHeight / logoHeight));
                                                        logoWidth = logoWidth * scale;
                                                        logoHeight = logoHeight * scale;
                                                        
                                                        // Center the logo horizontally
                                                        float logoX = printableCenterX - (logoWidth / 2);
                                                        float logoY = y;
                                                        
                                                        // Draw the logo
                                                        e.Graphics.DrawImage(logoImage, logoX, logoY, logoWidth, logoHeight);
                                                        
                                                        // Move Y position down after logo
                                                        y += logoHeight + 10f; // Add spacing after logo
                                                        justPrintedLogo = true; // Mark that we just printed the logo
                                                        
                                                        // Check for page break after logo
                                                        if (y > maxY)
                                                        {
                                                            e.HasMorePages = true;
                                                            currentLineIndex = i + 1;
                                                            return;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                catch
                                {
                                    // If logo fails to load, just skip it and continue
                                }
                                continue; // Skip normal rendering for logo marker
                            }
                            
                            // Render full-width separators for lines of '-' or '='
                            if (trimmedLine.All(ch => ch == '-') && trimmedLine.Length >= 10)
                            {
                                using (var pen = new Pen(Color.Black, 1f))
                                {
                                    var yLine = y + 2f;
                                    e.Graphics.DrawLine(pen, leftMargin, yLine, e.PageBounds.Width - rightMargin, yLine);
                                }
                                y += 8f;
                                inDeliveryAddressBlock = false;
                                
                                // Check for page break
                                if (y > maxY)
                                {
                                    e.HasMorePages = true;
                                    currentLineIndex = i + 1;
                                    return;
                                }
                                continue;
                            }
                            if (trimmedLine.All(ch => ch == '=') && trimmedLine.Length >= 10)
                            {
                                using (var pen = new Pen(Color.Black, 2f))
                                {
                                    var yLine1 = y + 1f;
                                    var yLine2 = y + 5f;
                                    e.Graphics.DrawLine(pen, leftMargin, yLine1, e.PageBounds.Width - rightMargin, yLine1);
                                    e.Graphics.DrawLine(pen, leftMargin, yLine2, e.PageBounds.Width - rightMargin, yLine2);
                                }
                                y += 10f;
                                inDeliveryAddressBlock = false;
                                
                                // Check for page break
                                if (y > maxY)
                                {
                                    e.HasMorePages = true;
                                    currentLineIndex = i + 1;
                                    return;
                                }
                                continue;
                            }
                            
                            // Determine font size and style based on content
                            Font font = new Font("Arial", 9); // Default font assignment
                            var brush = new SolidBrush(Color.Black);

                            // EARLY: Render delivery address lines in a consistent small font before any other styling
                            if (inDeliveryAddressBlock)
                            {
                                var addrLine = trimmedLine.Replace("**", "").Trim();
                                if (!string.IsNullOrEmpty(addrLine) && !trimmedLine.Contains("|"))
                                {
                                    // Skip only if this is a true separator (many dashes), not a hyphenated address
                                    int dashCount = addrLine.Count(ch => ch == '-');
                                    if (dashCount < 6)
                                    {
                                        using (var addrFont = new Font("Arial", 10, FontStyle.Bold))
                                        {
                                            var size = e.Graphics.MeasureString(addrLine, addrFont);
                                            var addrX = printableCenterX - (size.Width / 2);
                                            e.Graphics.DrawString(addrLine, addrFont, brush, addrX, y);
                                            y += size.Height + 3;
                                            
                                            // Check for page break
                                            if (y > maxY)
                                            {
                                                e.HasMorePages = true;
                                                currentLineIndex = i + 1;
                                                return;
                                            }
                                        }
                                        continue;
                                    }
                                }
                            }
                            
                             // Check for Sales Breakdown or Tender Summary headings (font size 10, bold)
                             if (trimmedLine.Contains("**Sales Breakdown**") || trimmedLine.Contains("**Tender Summary POS and Webshops**")
                             || trimmedLine.Contains("**Refund Summary - Paid and Canceled**") || trimmedLine.Contains("**Voids & Cancelled Orders**") || trimmedLine.Contains("**Adjustments & Discounts**") 
                             || trimmedLine.Contains("**Cash Drawer Summary**"))
                             {
                                 var headingText = trimmedLine.Replace("**", "");
                                 using (var headingFont = new Font("Arial", 10, FontStyle.Bold))
                                 {
                                     var headingSize = e.Graphics.MeasureString(headingText, headingFont);
                                     
                                     // Check if heading will fit on current page
                                     if (y + headingSize.Height + 6 > maxY)
                                     {
                                         e.HasMorePages = true;
                                         currentLineIndex = i;
                                         return;
                                     }
                                     
                                     var headingX = printableCenterX - (headingSize.Width / 2);
                                     e.Graphics.DrawString(headingText, headingFont, brush, headingX, y);
                                     y += headingSize.Height + 6;
                                     
                                     // Check for page break after drawing
                                     if (y > maxY)
                                     {
                                         e.HasMorePages = true;
                                         currentLineIndex = i + 1;
                                         return;
                                     }
                                 }
                                 continue;
                             }
                             
                             // Check for pickup time line first (before general ** handling)
                             if (trimmedLine.Contains("**") && !trimmedLine.Contains("|") && !trimmedLine.Contains("TOTAL") && (trimmedLine.Contains(":") && (trimmedLine.Contains("pm") || trimmedLine.Contains("am") || trimmedLine.Contains("PM") || trimmedLine.Contains("AM"))))
                             {
                                                                   // Special formatting for pickup time with border
                                  font = new Font("Arial", 10, FontStyle.Bold); // Increased from 9 to 10 for bigger time font
                                 
                                 // Remove the ** markers for drawing
                                 var timeText = trimmedLine.Replace("**", "");
                                 
                                 // Calculate text size and position for centering
                                 var timeTextSize = e.Graphics.MeasureString(timeText, font);
                                  var timeX = printableCenterX - (timeTextSize.Width / 2);
                                 
                                                                   // Draw border rectangle (curved corners)
                                  var borderPadding = 12f; // Increased from 8f to 12f for wider box
                                  var borderWidth = timeTextSize.Width + (borderPadding * 2);
                                  var borderHeight = timeTextSize.Height + (borderPadding * 2);
                                  var borderX = printableCenterX - (borderWidth / 2);
                                  var borderY = y - 2f; // Reduced from borderPadding to 2f to bring box closer to previous line
                                  
                                  // Draw rounded rectangle border
                                  var borderRect = new RectangleF(borderX, borderY, borderWidth, borderHeight);
                                  var borderPen = new Pen(Color.Black, 2f); // Increased from 1f to 2f for thicker border
                                 e.Graphics.DrawRoundedRectangle(borderRect, 5f, borderPen);
                                 
                                                                   // Draw the time text - adjust Y position to center within the box
                                  var textY = borderY + borderPadding; // Center the text vertically in the box
                                  e.Graphics.DrawString(timeText, font, brush, timeX, textY);
                                 
                                                                   // Clean up
                                  borderPen.Dispose();
                                  y += borderHeight + 8; // Increased from 3 to 8 for more space after the box
                                  font.Dispose();
                                 continue; // Skip normal drawing
                            }
                            // Kitchen receipt: title centered big
                            else if (trimmedLine.StartsWith(":KITCHEN RECEIPT:"))
                            {
                                inKitchenReceipt = true;
                                inItemSection = false; // Clear item section flag on receipt header
                                var titleText = trimmedLine.Replace(":KITCHEN RECEIPT:", "");
                                using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                                {
                                    var titleSize = e.Graphics.MeasureString(titleText, titleFont);
                                    var titleX = printableCenterX - (titleSize.Width / 2);
                                    e.Graphics.DrawString(titleText, titleFont, brush, titleX, y);
                                    y += titleSize.Height + 8;
                                }
                                continue;
                            }
                            // Z Report: title centered big
                            else if (trimmedLine.StartsWith("**Z REPORT**"))
                            {
                                inZReport = true;
                                using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                                {
                                    var titleText = trimmedLine.Replace("**", "");
                                    var titleSize = e.Graphics.MeasureString(titleText, titleFont);
                                    var titleX = printableCenterX - (titleSize.Width / 2);
                                    e.Graphics.DrawString(titleText, titleFont, brush, titleX, y);
                                    y += titleSize.Height + 8;
                                }
                                continue;
                            }
                            // Refund receipt: title centered, bold, larger font
                            else if (trimmedLine.StartsWith("**REFUND RECEIPT**"))
                            {
                                using (var titleFont = new Font("Arial", 18, FontStyle.Bold))
                                {
                                    var titleText = trimmedLine.Replace("**", "");
                                    var titleSize = e.Graphics.MeasureString(titleText, titleFont);
                                    var titleX = printableCenterX - (titleSize.Width / 2);
                                    e.Graphics.DrawString(titleText, titleFont, brush, titleX, y);
                                    y += titleSize.Height + 8;
                                }
                                continue;
                            }
                            // Kitchen receipt: STARTERS header centered big
                            else if (inKitchenReceipt && trimmedLine.Trim().Equals("**STARTERS**", StringComparison.Ordinal))
                            {
                                inItemSection = false; // Clear item section flag on section headers
                                using (var startersFont = new Font("Arial", 14, FontStyle.Bold))
                                {
                                    var startersText = trimmedLine.Replace("**", "");
                                    var startersSize = e.Graphics.MeasureString(startersText, startersFont);
                                    var startersX = printableCenterX - (startersSize.Width / 2);
                                    e.Graphics.DrawString(startersText, startersFont, brush, startersX, y);
                                    y += startersSize.Height + 6;
                                }
                                continue;
                            }
                            // Kitchen receipt: ITEMS header centered big
                            else if (inKitchenReceipt && trimmedLine.Trim().Equals("**ITEMS:**", StringComparison.Ordinal))
                            {
                                inItemSection = false; // Clear item section flag on section headers
                                using (var itemsFont = new Font("Arial", 14, FontStyle.Bold))
                                {
                                    var itemsText = trimmedLine.Replace("**", "");
                                    var itemsSize = e.Graphics.MeasureString(itemsText, itemsFont);
                                    var itemsX = printableCenterX - (itemsSize.Width / 2);
                                    e.Graphics.DrawString(itemsText, itemsFont, brush, itemsX, y);
                                    y += itemsSize.Height + 6;
                                }
                                continue;
                            }
                            // Kitchen receipt: Platform | OrderId header: both big bold on one line
                            else if (inKitchenReceipt && trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && trimmedLine.Contains("|"))
                            {
                                var headerText = trimmedLine.Replace("**", "");
                                var parts = headerText.Split('|');
                                if (parts.Length == 2)
                                {
                                    var left = parts[0].Trim();
                                    var right = parts[1].Trim();
                                    using (var hdrFont = new Font("Arial", 14, FontStyle.Bold))
                                    {
                                        // Left text (platform)
                                        e.Graphics.DrawString(left, hdrFont, brush, leftMargin, y);
                                        // Right text (display id)
                                        var rightSize = e.Graphics.MeasureString(right, hdrFont);
                                        var rightX = e.PageBounds.Width - rightMargin - rightSize.Width;
                                        e.Graphics.DrawString(right, hdrFont, brush, rightX, y);
                                        y += hdrFont.GetHeight() + 6;
                                    }
                                    continue;
                                }
                            }
                            // Kitchen receipt: Order type (centered, big bold)
                            else if (inKitchenReceipt && (trimmedLine.StartsWith("**TAKE AWAY**") || trimmedLine.StartsWith("**DINE IN**") || trimmedLine.StartsWith("**DELIVERY**")))
                            {
                                inItemSection = false; // Clear item section flag on order type
                                using (var orderTypeFont = new Font("Arial", 16, FontStyle.Bold))
                                {
                                    var orderTypeText = trimmedLine.Replace("**", "");
                                    var orderTypeSize = e.Graphics.MeasureString(orderTypeText, orderTypeFont);
                                    var orderTypeX = printableCenterX - (orderTypeSize.Width / 2);
                                    e.Graphics.DrawString(orderTypeText, orderTypeFont, brush, orderTypeX, y);
                                    y += orderTypeSize.Height + 8;
                                }
                                continue;
                            }
                            // Kitchen receipt: Wrapped platform name lines (left-aligned, same font as first line)
                            // Only catch if NOT in item section (to avoid intercepting wrapped item lines)
                            else if (inKitchenReceipt && !inItemSection && trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && !trimmedLine.Contains("|") && 
                                     !trimmedLine.StartsWith("**[") && !trimmedLine.Contains("ITEMS") && !trimmedLine.Contains("STARTERS") &&
                                     !trimmedLine.Contains("KITCHEN RECEIPT") && !trimmedLine.Contains("Table:") && !trimmedLine.Contains("Table No:") &&
                                     !trimmedLine.StartsWith("**TAKE AWAY**") && !trimmedLine.StartsWith("**DINE IN**") && !trimmedLine.StartsWith("**DELIVERY**"))
                            {
                                // This is likely a wrapped platform name line - left-align it like the first line
                                using (var hdrFont = new Font("Arial", 14, FontStyle.Bold))
                                {
                                    var wrappedText = trimmedLine.Replace("**", "");
                                    e.Graphics.DrawString(wrappedText, hdrFont, brush, leftMargin, y);
                                    y += hdrFont.GetHeight() + 6;
                                }
                                continue;
                            }
                            // Kitchen receipt: Wrapped item lines (continuation lines without | separator)
                            // ONLY apply when inItemSection flag is set to avoid affecting other receipt parts
                            else if (inKitchenReceipt && inItemSection && trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && 
                                     !trimmedLine.Contains("|") && !trimmedLine.StartsWith("**[") && // Not a new item line
                                     !trimmedLine.Contains(":") && // No colons (excludes labels)
                                     !trimmedLine.Contains("ITEMS") && !trimmedLine.Contains("STARTERS") &&
                                     !trimmedLine.Contains("KITCHEN RECEIPT") && !trimmedLine.Contains("Table:") &&
                                     !trimmedLine.Contains("Table No:") && !trimmedLine.StartsWith("**TAKE AWAY") &&
                                     !trimmedLine.StartsWith("**DINE IN") && !trimmedLine.StartsWith("**DELIVERY") &&
                                     !trimmedLine.Replace("**", "").All(c => c == '-' || c == ' ') && // Not a separator line
                                     trimmedLine.Length > 5 && trimmedLine.Length < 50) // Reasonable length for wrapped text
                            {
                                // This is a wrapped item continuation line - use same font as first line (Arial 11)
                                using (var itemFont = new Font("Arial", 11, FontStyle.Bold))
                                {
                                    var wrappedText = trimmedLine.Replace("**", "");
                                    e.Graphics.DrawString(wrappedText, itemFont, brush, leftMargin, y);
                                    y += itemFont.GetHeight() + 3;
                                }
                                continue; // Skip the normal drawing for this line
                            }
                            // Kitchen receipt: Order ID centered big (fallback when no platform)
                            else if (inKitchenReceipt && (trimmedLine.StartsWith("**ORDER #:") || trimmedLine.StartsWith("**Order #:") || (trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && trimmedLine.Length > 4 && trimmedLine.IndexOf('|') == -1 && trimmedLine.IndexOf(' ') == -1)))
                            {
                                using (var idFont = new Font("Arial", 14, FontStyle.Bold))
                                {
                                    var idText = trimmedLine.Replace("**", "");
                                    var idSize = e.Graphics.MeasureString(idText, idFont);
                                    var idX = printableCenterX - (idSize.Width / 2);
                                    e.Graphics.DrawString(idText, idFont, brush, idX, y);
                                    y += idSize.Height + 6;
                                }
                                continue;
                            }
                            // Kitchen receipt: Item line big bold (left aligned)
                            else if (inKitchenReceipt && trimmedLine.StartsWith("**[") && !trimmedLine.Contains("|"))
                            {
                                inItemSection = true; // Set flag - we're in an item section, next line might be wrapped item name
                                using (var itemFont = new Font("Arial", 11, FontStyle.Bold))
                                {
                                    var itemText = trimmedLine.Replace("**", "");
                                    e.Graphics.DrawString(itemText, itemFont, brush, leftMargin, y);
                                    y += itemFont.GetHeight() + 3;
                                }
                                continue;
                            }
                            // Z Report: platform header bold, left aligned with word wrap
                            else if (inZReport && trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && !trimmedLine.Contains("|") && !trimmedLine.Equals("**Z REPORT**", StringComparison.Ordinal))
                            {
                                var headerText = trimmedLine.Replace("**", "").Trim();
                                using (var hdrFont = new Font("Arial", 12, FontStyle.Bold))
                                {
                                    var words = headerText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    var current = string.Empty;
                                    for (int wi = 0; wi < words.Length; wi++)
                                    {
                                        var candidate = string.IsNullOrEmpty(current) ? words[wi] : current + " " + words[wi];
                                        var size = e.Graphics.MeasureString(candidate, hdrFont);
                                        if (size.Width <= pageWidth)
                                        {
                                            current = candidate;
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(current))
                                            {
                                                e.Graphics.DrawString(current, hdrFont, brush, leftMargin, y);
                                                y += hdrFont.GetHeight() + 4;
                                            }
                                            current = words[wi];
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(current))
                                    {
                                        e.Graphics.DrawString(current, hdrFont, brush, leftMargin, y);
                                        y += hdrFont.GetHeight() + 6;
                                    }
                                }
                                continue;
                            }
                            // Kitchen receipt: Modifier line slightly larger with indent (left aligned) and smart wrapping
                            else if (inKitchenReceipt && (trimmedLine.StartsWith("  + ") || trimmedLine.StartsWith("+ ")))
                            {
                                inItemSection = false; // Clear item section flag when encountering modifiers
                                using (var modFont = new Font("Arial", 10, FontStyle.Regular))
                                {
                            var modText = trimmedLine.Replace("**", "");
                            if (modText.StartsWith("+ ")) modText = modText.Substring(2);
                                    var modX = leftMargin + 10f;
                                    var availableWidth = pageWidth - (modX - leftMargin);

                                    // Word-wrap within available width preserving indentation
                                    var words = modText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                    var current = string.Empty;
                                    for (int wi = 0; wi < words.Length; wi++)
                                    {
                                        var candidate = string.IsNullOrEmpty(current) ? words[wi] : current + " " + words[wi];
                                        var size = e.Graphics.MeasureString(candidate, modFont);
                                        if (size.Width <= availableWidth)
                                        {
                                            current = candidate;
                                        }
                                        else
                                        {
                                            // Flush current line
                                            if (!string.IsNullOrEmpty(current))
                                            {
                                                e.Graphics.DrawString(current, modFont, brush, modX, y);
                                                y += modFont.GetHeight() + 2;
                                            }
                                            // Start new line with this word (may itself exceed width; if so, draw and continue)
                                            var longWord = words[wi];
                                            if (longWord.StartsWith("+")) longWord = longWord.TrimStart('+', ' ');
                                            var longSize = e.Graphics.MeasureString(longWord, modFont);
                                            if (longSize.Width <= availableWidth)
                                            {
                                                current = longWord;
                                            }
                                            else
                                            {
                                                // Hard draw the long word on its own line, then reset current
                                                e.Graphics.DrawString(longWord, modFont, brush, modX, y);
                                                y += modFont.GetHeight() + 2;
                                                current = string.Empty;
                                            }
                                        }
                                    }
                                    if (!string.IsNullOrEmpty(current))
                                    {
                                        e.Graphics.DrawString(current, modFont, brush, modX, y);
                                        y += modFont.GetHeight() + 2;
                                    }
                                }
                                continue;
                              }
                                                           // Check for separator line specifically (before customer name check)
                              else if (trimmedLine.Contains("**") && trimmedLine.Contains("-") && trimmedLine.Contains("-"))
                              {
                                  inItemSection = false; // Clear item section flag on separator lines
                                  // Special formatting for separator line after order type
                                  font = new Font("Arial", 12, FontStyle.Bold); // Bold font for separator
                                  
                                  // Remove the ** markers for drawing
                                  var separatorText = trimmedLine.Replace("**", "");
                                  
                                  // Calculate text size and position for centering
                                  var separatorTextSize = e.Graphics.MeasureString(separatorText, font);
                                  var separatorX = printableCenterX - (separatorTextSize.Width / 2);
                                  
                                  // Draw the separator text centered
                                  e.Graphics.DrawString(separatorText, font, brush, separatorX, y);
                                  
                                  y += separatorTextSize.Height + 8; // Add extra space after separator
                                  font.Dispose();
                                  // End any delivery address block when a separator is reached
                                  inDeliveryAddressBlock = false;
                                  continue; // Skip normal drawing
                              }
                             else if (trimmedLine.StartsWith("**TAKE AWAY**") || trimmedLine.StartsWith("**DINE IN**") || trimmedLine.StartsWith("**DELIVERY**"))
                             {
                                 inPlatformNameSection = false; // End platform name section so customer name / tax summary are not styled as platform (e.g. when "Order placed" line is omitted)
                                 // Special formatting for order type with big bold letters
                                 font = new Font("Arial", 16, FontStyle.Bold); // Big bold font for order type
                                 
                                 // Remove the ** markers for drawing
                                 var orderTypeText = trimmedLine.Replace("**", "");
                                 
                                 // Calculate text size and position for centering
                                 var orderTypeTextSize = e.Graphics.MeasureString(orderTypeText, font);
                                   var orderTypeX = printableCenterX - (orderTypeTextSize.Width / 2);
                                 
                                 // Draw the order type text centered
                                 e.Graphics.DrawString(orderTypeText, font, brush, orderTypeX, y);
                                 
                                 y += orderTypeTextSize.Height + 8; // Add extra space after order type
                                 font.Dispose();
                                 // Enable delivery address block styling after the DELIVERY header
                                 if (string.Equals(orderTypeText.Trim(), "DELIVERY", StringComparison.OrdinalIgnoreCase))
                                 {
                                     inDeliveryAddressBlock = true;
                                 }
                                 continue; // Skip normal drawing
                             }
                            else if (trimmedLine.Contains("**") && (trimmedLine.Contains("Table:") || trimmedLine.Contains("Table No:")))
                             {
                                 inPlatformNameSection = false; // End platform name section so customer name is not styled as platform (e.g. when "Order placed" line is omitted for Dine In)
                                 // Special formatting for table number with big bold letters (like order type)
                                font = new Font("Arial", 14, FontStyle.Bold); // Big bold font for table line
                                 
                                 // Remove the ** markers for drawing
                                 var tableNumberText = trimmedLine.Replace("**", "");
                                 
                                 // Calculate text size and position for centering
                                 var tableNumberTextSize = e.Graphics.MeasureString(tableNumberText, font);
                                 var tableNumberX = printableCenterX - (tableNumberTextSize.Width / 2);
                                 
                                 // Draw the table number text centered
                                 e.Graphics.DrawString(tableNumberText, font, brush, tableNumberX, y);
                                 
                                 y += tableNumberTextSize.Height + 8; // Add extra space after table number
                                 font.Dispose();
                                 continue; // Skip normal drawing
                             }
                                                             else if (trimmedLine.Contains("**PAYMENT:**") && trimmedLine.Contains("|"))
                               {
                                   // Special formatting for payment method with border (like pickup time)
                                   font = new Font("Arial", 12, FontStyle.Bold); // Font size 12, bold for payment method
                                   
                                   // Remove the ** markers and get payment method text
                                   var paymentText = trimmedLine.Replace("**", "").Split('|')[1].Trim();
                                   
                                   // Calculate text size and position for centering
                                   var paymentTextSize = e.Graphics.MeasureString(paymentText, font);
                                   var paymentX = printableCenterX - (paymentTextSize.Width / 2);
                                   
                                   // Draw border rectangle (curved corners) - wider box
                                   var borderPadding = 12f; // Same as pickup time
                                   var borderWidth = paymentTextSize.Width + (borderPadding * 2) + 40f; // Add 40f extra width
                                   var borderHeight = paymentTextSize.Height + (borderPadding * 2);
                                   var borderX = printableCenterX - (borderWidth / 2);
                                   var borderY = y - 2f; // Same as pickup time
                                   
                                   // Draw rounded rectangle border
                                   var borderRect = new RectangleF(borderX, borderY, borderWidth, borderHeight);
                                   var borderPen = new Pen(Color.Black, 2f); // Same as pickup time
                                   e.Graphics.DrawRoundedRectangle(borderRect, 5f, borderPen);
                                   
                                   // Draw the payment text - adjust Y position to center within the box
                                   var textY = borderY + borderPadding; // Center the text vertically in the box
                                   e.Graphics.DrawString(paymentText, font, brush, paymentX, textY);
                                   
                                   // Clean up
                                   borderPen.Dispose();
                                   y += borderHeight + 8; // Same spacing as pickup time
                                   font.Dispose();
                                   continue; // Skip normal drawing
                               }
                                                                                          else if (trimmedLine.StartsWith("Contact:") && !trimmedLine.Contains("**"))
                               {
                                   // Special formatting for customer phone with font size 11 (not bold)
                                   font = new Font("Arial", 11, FontStyle.Regular); // Font size 11, not bold for phone
                                   
                                   // Calculate text size and position for centering
                                   var customerPhoneTextSize = e.Graphics.MeasureString(trimmedLine, font);
                                   var customerPhoneX = printableCenterX - (customerPhoneTextSize.Width / 2);
                                   
                                   // Draw the customer phone text centered
                                   e.Graphics.DrawString(trimmedLine, font, brush, customerPhoneX, y);
                                   
                                   y += customerPhoneTextSize.Height + 8; // Add extra space after customer phone
                                   font.Dispose();
                                   continue; // Skip normal drawing
                               }
                                                             else if (trimmedLine.Contains("**Order Note:** "))
                               {
                                   inItemSection = false; // Clear item section flag on order notes
                                   // Mixed formatting: label bold size 11, content regular size 11; centered with pixel-accurate wrapping
                                   var lineWithoutMarkers = trimmedLine.Replace("**", "");
                                   var splitIndex = lineWithoutMarkers.IndexOf(": ");
                                   var label = lineWithoutMarkers.Substring(0, splitIndex + 1); // includes colon
                                   var contentPart = lineWithoutMarkers.Substring(splitIndex + 2);

                                  using (var labelFont = new Font("Arial", 11, FontStyle.Bold))
                                  using (var contentFont = new Font("Arial", 11, FontStyle.Regular))
                                  {
                                      var availableWidth = pageWidth; // already accounts for margins
                                      var labelSize = e.Graphics.MeasureString(label + " ", labelFont);

                                      // Fit as many words as possible on the first line (considering label width)
                                      var words = contentPart.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                      var firstLineContent = string.Empty;
                                      int wordIndex = 0;
                                      while (wordIndex < words.Length)
                                      {
                                          var candidate = string.IsNullOrEmpty(firstLineContent) ? words[wordIndex] : firstLineContent + " " + words[wordIndex];
                                          var candidateSize = e.Graphics.MeasureString(candidate, contentFont);
                                          if (labelSize.Width + candidateSize.Width <= availableWidth)
                                          {
                                              firstLineContent = candidate;
                                              wordIndex++;
                                          }
                                          else
                                          {
                                              break;
                                          }
                                      }

                                      // Draw first line (label + first chunk)
                                      var firstChunkSize = e.Graphics.MeasureString(firstLineContent, contentFont);
                                      var totalWidth = labelSize.Width + firstChunkSize.Width;
                                      var startX = printableCenterX - (totalWidth / 2);
                                      e.Graphics.DrawString(label + " ", labelFont, brush, startX, y);
                                      e.Graphics.DrawString(firstLineContent, contentFont, brush, startX + labelSize.Width, y);
                                      var firstLineHeight = Math.Max(labelSize.Height, firstChunkSize.Height);
                                      y += firstLineHeight + 8;

                                      // Draw remaining words as centered continuation lines with content font
                                      var currentLine = string.Empty;
                                      for (; wordIndex < words.Length; wordIndex++)
                                      {
                                          var nextCandidate = string.IsNullOrEmpty(currentLine) ? words[wordIndex] : currentLine + " " + words[wordIndex];
                                          var nextSize = e.Graphics.MeasureString(nextCandidate, contentFont);
                                          if (nextSize.Width <= availableWidth)
                                          {
                                              currentLine = nextCandidate;
                                          }
                                          else
                                          {
                                              // Flush current line
                                              if (!string.IsNullOrEmpty(currentLine))
                                              {
                                                  var lineSize = e.Graphics.MeasureString(currentLine, contentFont);
                                                  var xCentered = printableCenterX - (lineSize.Width / 2);
                                                  e.Graphics.DrawString(currentLine, contentFont, brush, xCentered, y);
                                                  y += lineSize.Height + 8;
                                              }
                                              // Start new line with the word that didn't fit
                                              currentLine = words[wordIndex];
                                          }
                                      }
                                      // Flush leftover
                                      if (!string.IsNullOrEmpty(currentLine))
                                      {
                                          var lineSize = e.Graphics.MeasureString(currentLine, contentFont);
                                          var xCentered = printableCenterX - (lineSize.Width / 2);
                                          e.Graphics.DrawString(currentLine, contentFont, brush, xCentered, y);
                                          y += lineSize.Height + 8;
                                      }
                                  }
                                  continue;
                              }
                              else if (trimmedLine.StartsWith(":ON: "))
                              {
                                  // Skip :ON: lines (legacy marker); first order-note branch handles wrapping/drawing
                                  continue;
                              }
                                                             else if (trimmedLine.Contains("**IMPORTANT:** Call the restaurant for allergy info"))
                               {
                                   // Mixed formatting: "IMPORTANT:" bold, rest regular; both size 10 and centered
                                   var lineWithoutMarkers = trimmedLine.Replace("**", "");
                                   var splitIndex = lineWithoutMarkers.IndexOf(": ");
                                   var label = lineWithoutMarkers.Substring(0, splitIndex + 1); // includes colon
                                   var contentPart = lineWithoutMarkers.Substring(splitIndex + 2);

                                  using (var labelFont = new Font("Arial", 9, FontStyle.Bold))
                                  using (var contentFont = new Font("Arial", 9, FontStyle.Regular))
                                  {
                                      var labelSize = e.Graphics.MeasureString(label + " ", labelFont);
                                      var contentSize = e.Graphics.MeasureString(contentPart, contentFont);
                                      var totalWidth = labelSize.Width + contentSize.Width;
                                      var startX = printableCenterX - (totalWidth / 2);

                                      // Draw label then content on same baseline
                                      e.Graphics.DrawString(label + " ", labelFont, brush, startX, y);
                                      e.Graphics.DrawString(contentPart, contentFont, brush, startX + labelSize.Width, y);

                                      var lineHeight = Math.Max(labelSize.Height, contentSize.Height);
                                      y += lineHeight + 8;
                                  }
                                  continue;
                              }
                                                           else if (trimmedLine.Contains("**DELIVERY ADDRESS:**"))
                             {
                                 // Start delivery address block header (centered, bold)
                                 inDeliveryAddressBlock = true;
                                 var headerText = trimmedLine.Replace("**", "");
                                 using (var headerFont = new Font("Arial", 12, FontStyle.Bold))
                                 {
                                     var headerSize = e.Graphics.MeasureString(headerText, headerFont);
                                     var headerX = printableCenterX - (headerSize.Width / 2);
                                     e.Graphics.DrawString(headerText, headerFont, brush, headerX, y);
                                     y += headerSize.Height + 6;
                                 }
                                 continue;
                             }
                             else if (inDeliveryAddressBlock && trimmedLine.Contains("**") && !trimmedLine.Contains("|"))
                             {
                                 // Delivery address lines: smaller than customer name, regular weight, centered
                                 var addressText = trimmedLine.Replace("**", "");
                                 using (var addrFont = new Font("Arial", 8, FontStyle.Regular))
                                 {
                                     var addrSize = e.Graphics.MeasureString(addressText, addrFont);
                                     var addrX = printableCenterX - (addrSize.Width / 2);
                                     e.Graphics.DrawString(addressText, addrFont, brush, addrX, y);
                                     y += addrSize.Height + 2;
                                  }
                                  continue;
                              }
                                                           // Platform shop text handler - ALL lines (first line with " - " and wrapped lines without " - ")
                                        // This handler catches ALL platform shop text lines and applies font size 11 consistently
                                        // Must run BEFORE other handlers to prevent overriding
                                        // Key: First line contains " - ", wrapped lines use flag (inPlatformShopTextSection)
                                        else if (trimmedLine.Contains("**") && !trimmedLine.Contains("|") && 
                                        !trimmedLine.Contains("TOTAL") && !trimmedLine.Contains("TAKE AWAY") && !trimmedLine.Contains("DINE IN") && 
                                        !trimmedLine.Contains("DELIVERY") && !trimmedLine.Contains("pm") && !trimmedLine.Contains("PM") && 
                                        !trimmedLine.Contains("AM") && !trimmedLine.StartsWith("Contact:") && !trimmedLine.Contains("PAYMENT:") && 
                                        !trimmedLine.Contains("SUBTOTAL:") && !trimmedLine.Contains("DISCOUNT:") && !trimmedLine.StartsWith("**COUPON") &&
                                        !trimmedLine.Contains("COUPON:") && !trimmedLine.Contains("DELIVERY:") && !trimmedLine.Contains(",") &&
                                        !trimmedLine.StartsWith("**[") && !trimmedLine.Contains("ITEMS") && !trimmedLine.Contains("STARTERS") &&
                                        !trimmedLine.Contains("KITCHEN RECEIPT") && !trimmedLine.Contains("Table:") && !trimmedLine.Contains("Table No:") &&
                                        (trimmedLine.Contains(" - ") || inPlatformShopTextSection)) // First line has " - ", wrapped lines use flag
                               {
                                   // Platform shop text - ALL lines use font size 11, bold, centered
                                   // First line contains " - " and sets the flag
                                   // Wrapped lines don't have " - " but flag is set
                                   if (trimmedLine.Contains(" - "))
                                   {
                                       // First line - set flag for wrapped lines
                                       inPlatformNameSection = false;
                                       inPlatformShopTextSection = true;
                                   }
                                   
                                   using (var platformShopFont = new Font("Arial", 11, FontStyle.Bold))
                                   {
                                       // Remove the ** markers for drawing
                                       var platformShopText = trimmedLine.Replace("**", "");
                                       
                                       // Calculate text size and position for centering
                                       var platformShopTextSize = e.Graphics.MeasureString(platformShopText, platformShopFont);
                                       var platformShopX = printableCenterX - (platformShopTextSize.Width / 2);
                                       
                                       // Draw the platform shop text centered
                                       e.Graphics.DrawString(platformShopText, platformShopFont, brush, platformShopX, y);
                                       
                                       y += platformShopTextSize.Height + 8; // Add extra space after platform shop text
                                   }
                                   continue; // Skip normal drawing - CRITICAL to prevent other handlers from catching this
                               }
                                                           else if (trimmedLine.Contains("**") && trimmedLine.Contains("**") && (y <= 20f || justPrintedLogo || trimmedLine.Contains("|") || inPlatformNameSection) && !inPlatformShopTextSection)
                             {
                                 // Check if it's a delivery platform name line (first line with ** or wrapped platform name)
                                 // Exclude platform shop text section to avoid overriding font size 10
                                 // Also check justPrintedLogo flag to handle case when logo was printed
                                 bool isPlatformNameLine = (y <= 20f || justPrintedLogo || inPlatformNameSection) && !trimmedLine.Contains("|") && 
                                                           !trimmedLine.Contains("TOTAL") && 
                                                           !trimmedLine.Contains("Order placed") &&
                                                           !trimmedLine.Contains("Pickup -") &&
                                                           !trimmedLine.Contains("Delivery -") &&
                                                           !trimmedLine.Contains("TAKE AWAY") &&
                                                           !trimmedLine.Contains("DINE IN") &&
                                                           !trimmedLine.Contains("DELIVERY");
                                 
                                 if (isPlatformNameLine)
                                 {
                                     // Extra large bold font for delivery platform name (including wrapped lines)
                                     font = new Font("Arial", 18, FontStyle.Bold);
                                     inPlatformNameSection = true; // Mark that we're in platform name section
                                 }
                                 else
                                 {
                                     // Slightly bigger bold font for main items (optimized for 80mm printer)
                                     font = new Font("Arial", 9, FontStyle.Bold);
                                 }
                                 
                                                                   // Handle combined lines with | separator (platform name + order ID, or item + price)
                                 if (trimmedLine.Contains("|"))
                                 {
                                     var parts = trimmedLine.Split('|');
                                     if (parts.Length == 2)
                                     {
                                          var leftText = parts[0].Replace("**", "").Trim();
                                          var rightText = parts[1].Replace("**", "").Trim();
                                          
                                          // Check if it's the platform name line (first line with **)
                                          // Also check justPrintedLogo flag to handle case when logo was printed
                                          if ((y <= 20f || justPrintedLogo) && !rightText.Contains("£") && !rightText.Contains("$") && !rightText.Contains("Rs") && !rightText.Contains("TOTAL"))
                                          {
                                              // Extra large bold font for platform name and order ID
                                              var platformFont = new Font("Arial", 18, FontStyle.Bold);
                                              
                                              // Draw platform name (left-aligned)
                                              e.Graphics.DrawString(leftText, platformFont, brush, leftMargin, y);
                                              
                                              // Draw order ID (right-aligned)
                                              var orderIdSize = e.Graphics.MeasureString(rightText, platformFont);
                                              var orderIdX = e.PageBounds.Width - rightMargin - orderIdSize.Width;
                                              e.Graphics.DrawString(rightText, platformFont, brush, orderIdX, y);
                                              
                                              y += platformFont.GetHeight() + 3;
                                              platformFont.Dispose();
                                              inPlatformNameSection = true; // Mark that we're in platform name section
                                              justPrintedLogo = false; // Reset flag after processing platform name line
                                              continue; // Skip the normal drawing for this line
                                          }
                                          else
                                          {
                                              // Totals-like labels: delegate to totals styling
                                              bool isTotalsLike =
                                                  leftText.StartsWith("SUBTOTAL", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("TOTAL FEE", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("BOGO DISCOUNT", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("DISCOUNT", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("COUPON", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.Contains("COUPON", StringComparison.OrdinalIgnoreCase) || // Also check if COUPON appears anywhere in the label
                                                  leftText.StartsWith("DELIVERY CHARGE", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("DELIVERY", StringComparison.OrdinalIgnoreCase) ||
                                                  leftText.StartsWith("TOTAL", StringComparison.OrdinalIgnoreCase);

                                              if (isTotalsLike)
                                              {
                                                  using (var totalsFont = new Font("Arial", 9, FontStyle.Bold))
                                                  {
                                                      // Left label
                                                      e.Graphics.DrawString(leftText, totalsFont, brush, leftMargin, y);
                                                      // Right value
                                                      var valueSize = e.Graphics.MeasureString(rightText, totalsFont);
                                                      var valueX = e.PageBounds.Width - rightMargin - valueSize.Width;
                                                      e.Graphics.DrawString(rightText, totalsFont, brush, valueX, y);
                                                      y += totalsFont.GetHeight() + 3;
                                                  }
                                                  continue; // handled here
                                              }

                                              // Regular item and price line
                                              inItemSection = true; // Set flag - we're in an item section, next line might be wrapped item name
                                              var itemFont = new Font("Arial", 9, FontStyle.Bold);
                                         // Draw item name (left-aligned)
                                              e.Graphics.DrawString(leftText, itemFont, brush, leftMargin, y);
                                         // Draw price (right-aligned)
                                              var priceSize = e.Graphics.MeasureString(rightText, itemFont);
                                         var priceX = e.PageBounds.Width - rightMargin - priceSize.Width;
                                              e.Graphics.DrawString(rightText, itemFont, brush, priceX, y);
                                         y += itemFont.GetHeight() + 3;
                                         itemFont.Dispose();
                                         continue; // Skip the normal drawing for this line
                                          }
                                     }
                                 }
                                 
                                 // For lines without | separator that set font size 9, we need to continue
                                 // to prevent it from falling through to normal drawing (which would use the wrong font)
                                 // BUT: if we're in platform shop text section, DON'T remove markers and let it fall through to our handler
                                 if (!isPlatformNameLine && !trimmedLine.Contains("|") && !inPlatformShopTextSection)
                                 {
                                     // This is a bold line that matched the condition but isn't a platform name line
                                     // Remove markers and continue to normal drawing with font size 9
                                     trimmedLine = trimmedLine.Replace("**", "");
                                     // Continue to normal drawing with the font we set (size 9)
                                 }
                                 else if (!isPlatformNameLine && !trimmedLine.Contains("|") && inPlatformShopTextSection)
                                 {
                                     // We're in platform shop text section - DON'T remove markers, let our handler catch it
                                     // Keep the ** markers so our platform shop text handler can match it
                                     // Don't do anything here, just let it fall through
                                 }
                                 else
                                 {
                                     // Remove the markers for display (for other bold lines)
                                     trimmedLine = trimmedLine.Replace("**", "");
                                 }
                             }
                             // Handle wrapped item lines (continuation lines without | separator)
                             // ONLY apply when inItemSection flag is set to avoid affecting other receipt parts
                             else if (inItemSection && !inKitchenReceipt && !inZReport && trimmedLine.StartsWith("**") && trimmedLine.EndsWith("**") && 
                                 !trimmedLine.Contains("|") && !trimmedLine.StartsWith("**[") && // Not a new item line
                                 !trimmedLine.Contains(":") && // No colons (excludes labels like "Order Note:", "Table:", etc.)
                                 !trimmedLine.StartsWith("**TAKE AWAY") && !trimmedLine.StartsWith("**DINE IN") && !trimmedLine.StartsWith("**DELIVERY") &&
                                 !trimmedLine.Contains("TOTAL") && !trimmedLine.Contains("SUBTOTAL") && !trimmedLine.Contains("DISCOUNT") &&
                                 !trimmedLine.Contains("COUPON") && !trimmedLine.Contains("DELIVERY") && !trimmedLine.Contains("Order Note") &&
                                 !trimmedLine.Contains("Note") && !trimmedLine.Contains("IMPORTANT") && !trimmedLine.Contains("Table") &&
                                 !trimmedLine.Contains("ITEMS") && !trimmedLine.Contains("STARTERS") && !trimmedLine.Contains("KITCHEN RECEIPT") &&
                                 !trimmedLine.Contains("Contact") && !trimmedLine.Contains("Printed") && !trimmedLine.Contains("Pickup") &&
                                 !trimmedLine.Contains("Order placed") && !trimmedLine.Contains("PAYMENT") && !inPlatformShopTextSection &&
                                 !trimmedLine.Contains("Thank you") && !trimmedLine.Contains("Please keep") && !trimmedLine.Contains("CASH") &&
                                 !trimmedLine.Contains("CARD") && !trimmedLine.Contains(",") && // No commas (excludes addresses)
                                 !trimmedLine.Replace("**", "").All(c => c == '-' || c == ' ') && // Not a separator line (all dashes/spaces)
                                 trimmedLine.Length > 5 && trimmedLine.Length < 50) // Reasonable length for wrapped text
                             {
                                 // This is a wrapped item continuation line - use same font as first line
                                 using (var itemFont = new Font("Arial", 9, FontStyle.Bold))
                                 {
                                     var wrappedText = trimmedLine.Replace("**", "");
                                     e.Graphics.DrawString(wrappedText, itemFont, brush, leftMargin, y);
                                     y += itemFont.GetHeight() + 3;
                                 }
                                 continue; // Skip the normal drawing for this line
                             }
                                                                                          else if (trimmedLine.Contains("Order placed") || trimmedLine.Contains("Pickup -") || trimmedLine.Contains("Delivery -"))
                                               {
                                                   inPlatformNameSection = false; // End platform name section so following lines (customer name, etc.) are not styled as platform
                                                   // Regular font for order placement time and pickup date
                                                   font = new Font("Arial", 9, FontStyle.Bold);
                                               }
                                                               
                                  
                              
                                // Refund receipt: order details and refund details - left-aligned, regular font only
                              else if (trimmedLine.StartsWith(":REGULAR:"))
                              {
                                  var regularText = trimmedLine.Substring(9); // Remove ":REGULAR:" prefix
                                  using (var regularFont = new Font("Arial", 9, FontStyle.Regular))
                                  {
                                      e.Graphics.DrawString(regularText, regularFont, brush, leftMargin, y);
                                      y += regularFont.GetHeight() + 3;
                                  }
                                  continue; // Skip normal drawing
                              }    
                             else if (trimmedLine.Contains("DELIVERY") || trimmedLine.Contains("PICKUP") || 
                                      trimmedLine.Contains("CUSTOMER DETAILS") || trimmedLine.Contains("ITEMS ORDERED") ||
                                      trimmedLine.Contains("ORDER SUMMARY") || trimmedLine.Contains("PAYMENT METHOD"))
                             {
                                 // Bold font for section headers
                                 font = new Font("Arial", 10, FontStyle.Bold);
                             }
                                                           else if (trimmedLine.Contains("**SUBTOTAL:**") || trimmedLine.Contains("**TOTAL:**") || 
                                       trimmedLine.Contains("**DISCOUNT:**") || trimmedLine.Contains("**COUPON:**") || 
                                       trimmedLine.Contains("**DELIVERY:**") || trimmedLine.Contains("**DELIVERY CHARGE:**") || 
                                       trimmedLine.Contains("**BOGO DISCOUNT:**") || trimmedLine.Contains("**TOTAL FEE:**") || 
                                       trimmedLine.Contains("**OTHER:**"))
                              {
                                  inItemSection = false; // Clear item section flag when entering totals section
                                  // Bold font for totals and discounts - handle like items with left/right alignment
                                  font = new Font("Arial", 9, FontStyle.Bold);
                                  
                                  // Handle combined lines with | separator (like items)
                                  if (trimmedLine.Contains("|"))
                                  {
                                      var parts = trimmedLine.Split('|');
                                      if (parts.Length == 2)
                                      {
                                          var leftText = parts[0].Replace("**", "").Trim();
                                          var rightText = parts[1].Replace("**", "").Trim();
                                          
                                          // Draw label (left-aligned)
                                          e.Graphics.DrawString(leftText, font, brush, leftMargin, y);
                                          
                                          // Draw amount (right-aligned)
                                          var amountSize = e.Graphics.MeasureString(rightText, font);
                                          var amountX = e.PageBounds.Width - rightMargin - amountSize.Width;
                                          e.Graphics.DrawString(rightText, font, brush, amountX, y);
                                          
                                          y += font.GetHeight() + 3;
                                          font.Dispose();
                                          continue; // Skip normal drawing
                                      }
                                  }
                             }
                             else if (trimmedLine.Contains("CASH") || trimmedLine.Contains("CARD"))
                             {
                                 // Bold font for payment method
                                 font = new Font("Arial", 10, FontStyle.Bold);
                             }
                                                           else if (trimmedLine.StartsWith("Thank you for ordering with"))
                              {
                                  // Font size 10 for thank you message
                                  // Reset platform name section flag when entering footer section
                                  inPlatformNameSection = false;
                                  font = new Font("Arial", 10, FontStyle.Regular);
                              }
                              else if (trimmedLine.StartsWith(":ITALIC:"))
                              {
                                  // Handle italic text (e.g., "Powered by Delivergate")
                                  var italicText = trimmedLine.Substring(8); // Remove ":ITALIC:" prefix
                                  using (var italicFontObj = new Font("Arial", 9, FontStyle.Italic))
                                  {
                                      var italicSize = e.Graphics.MeasureString(italicText, italicFontObj);
                                      var italicX = printableCenterX - (italicSize.Width / 2);
                                      e.Graphics.DrawString(italicText, italicFontObj, brush, italicX, y);
                                      y += italicSize.Height;
                                  }
                                  continue; // Skip normal drawing
                              }
                                                             else if (trimmedLine.Contains("**") && !trimmedLine.Contains("|") && !trimmedLine.Contains("TOTAL") && 
                                        !trimmedLine.Contains("TAKE AWAY") && !trimmedLine.Contains("DINE IN") && !trimmedLine.Contains("DELIVERY") && 
                                        !trimmedLine.Contains("pm") && !trimmedLine.Contains("PM") && !trimmedLine.Contains("AM") && 
                                        !trimmedLine.StartsWith("Contact:") && !trimmedLine.Contains("PAYMENT:") && 
                                        !trimmedLine.Contains("SUBTOTAL:") && !trimmedLine.Contains("DISCOUNT:") && 
                                        !trimmedLine.Contains("COUPON:") && !trimmedLine.Contains("DELIVERY:") &&
                                        trimmedLine.Length >= 20 && trimmedLine.Contains(",")) // Shop address is longer and contains commas
                               {
                                   // Reset platform shop text section flag when we encounter shop address
                                   inPlatformShopTextSection = false;
                                   
                                   // Special formatting for shop address with font size 9, bold, centered
                                   font = new Font("Arial", 9, FontStyle.Bold); // Font size 9, bold for shop address
                                   
                                   // Remove the ** markers for drawing
                                   var shopAddressText = trimmedLine.Replace("**", "");
                                   
                                   // Calculate text size and position for centering
                                   var shopAddressTextSize = e.Graphics.MeasureString(shopAddressText, font);
                                   var shopAddressX = printableCenterX - (shopAddressTextSize.Width / 2);
                                   
                                   // Draw the shop address text centered
                                   e.Graphics.DrawString(shopAddressText, font, brush, shopAddressX, y);
                                   
                                   y += shopAddressTextSize.Height + 8; // Add extra space after shop address
                                   font.Dispose();
                                   continue; // Skip normal drawing
                             }
                             else if (trimmedLine.StartsWith("Thank you") || trimmedLine.Contains("Please keep"))
                             {
                                 // Medium font for footer
                                 font = new Font("Arial", 8, FontStyle.Bold);
                             }
                             else if (trimmedLine.StartsWith("  ") && !trimmedLine.StartsWith("  |"))
                             {
                                 inItemSection = false; // Clear item section flag when encountering modifiers/notes
                                 // Smaller font for modifiers and notes
                                 font = new Font("Arial", 8);
                             }
                              
                                                           // Tax registration (shop details) — centered under tax summary heading
                              else if (trimmedLine.StartsWith("**", StringComparison.Ordinal) && trimmedLine.EndsWith("**", StringComparison.Ordinal) &&
                                       trimmedLine.Contains("TAX REG NO", StringComparison.OrdinalIgnoreCase) && !trimmedLine.Contains("|"))
                              {
                                  var taxRegText = trimmedLine.Replace("**", "").Trim();
                                  using (var taxRegFont = new Font("Arial", 8, FontStyle.Bold))
                                  {
                                      var taxRegSize = e.Graphics.MeasureString(taxRegText, taxRegFont);
                                      var taxRegX = printableCenterX - (taxRegSize.Width / 2f);
                                      e.Graphics.DrawString(taxRegText, taxRegFont, brush, taxRegX, y);
                                      y += taxRegSize.Height + 3f;
                                  }
                                  continue;
                              }
                                                           // Check for customer name specifically (before asterisk condition)
                              else if (trimmedLine.Contains("**") && !trimmedLine.Contains("|") && !trimmedLine.Contains("TOTAL") && !trimmedLine.Contains("TAKE AWAY") && !trimmedLine.Contains("DINE IN") && !trimmedLine.Contains("DELIVERY") && !trimmedLine.Contains("pm") && !trimmedLine.Contains("PM") && !trimmedLine.Contains("AM") && !trimmedLine.StartsWith("Contact:") && !inPlatformShopTextSection)
                              {
                                  // Additional check: ensure it's a simple customer name (not too long, no special formatting)
                                  var customerNameText = trimmedLine.Replace("**", "").Trim();
                                  
                                  // Check if it looks like a customer name (simple text, reasonable length, not a separator)
                                  if (customerNameText.Length > 0 && customerNameText.Length <= 50 && !customerNameText.Contains("£") && !customerNameText.Contains("$") && !customerNameText.StartsWith("-") && !customerNameText.EndsWith("-"))
                                  {
                                      // Special formatting for customer name with font size 11
                                      font = new Font("Arial", 11, FontStyle.Bold); // Font size 11 for customer name
                                      
                                      // Calculate text size and position for centering
                                      var customerNameTextSize = e.Graphics.MeasureString(customerNameText, font);
                                      var customerNameX = printableCenterX - (customerNameTextSize.Width / 2);
                                      
                                      // Draw the customer name text centered
                                      e.Graphics.DrawString(customerNameText, font, brush, customerNameX, y);
                                      
                                      y += customerNameTextSize.Height + 8; // Add extra space after customer name
                                      font.Dispose();
                                      continue; // Skip normal drawing
                                  }
                              }
                                                           else if ((trimmedLine.Contains("*") || trimmedLine.Contains("-")) && !trimmedLine.Contains("**"))
                              {
                                  // Small font for separators (but not lines with ** markers)
                                 font = new Font("Arial", 7);
                             }
                             else
                             {
                                 // Default font for regular text, or font size 11 for table rows
                                 if (inTable11)
                                 {
                                     font = new Font("Arial", 9, FontStyle.Regular);
                                 }
                                 else
                                 {
                                     font = new Font("Arial", 9);
                                 }
                             }
                            
                                                         // Calculate text position
                             var textSize = e.Graphics.MeasureString(trimmedLine, font);
                             var x = leftMargin;
                             
                             // Center text if it's a header, payment method, or shop details
                             if (trimmedLine.Contains("DELIVERY") || trimmedLine.Contains("CASH") || 
                                 trimmedLine.Contains("CARD") || trimmedLine.Contains("Thank you") ||
                                 trimmedLine.Contains("Please keep") || trimmedLine.Contains("Phone:") ||
                                 trimmedLine.Contains("Email:") || trimmedLine.Contains("Date:") ||
                                 trimmedLine.Contains("Time:") || trimmedLine.Contains("Order #:") ||
                                 trimmedLine.Contains("Order Type:") || trimmedLine.Contains("Cashier:") ||
                                 trimmedLine.Contains("CUSTOMER DETAILS") || trimmedLine.Contains("ITEMS ORDERED") ||
                                 trimmedLine.Contains("ORDER SUMMARY") || trimmedLine.Contains("PAYMENT METHOD") ||
                                  trimmedLine.Contains("ORDER NOTE") || trimmedLine.Contains("Tel:") ||
                                  trimmedLine.Contains("Order placed") || trimmedLine.Contains("Pickup -") || trimmedLine.Contains("Delivery -"))
                             {
                                x = printableCenterX - (textSize.Width / 2); // Center within printable area
                             }
                             // Right align prices (lines that contain only currency symbols and numbers)
                             else if ((trimmedLine.Contains("£") || trimmedLine.Contains("$") || trimmedLine.Contains("Rs")) && 
                                      !trimmedLine.StartsWith("  ") && !trimmedLine.StartsWith("|") &&
                                      (trimmedLine.Contains("TOTAL") || trimmedLine.Contains("Subtotal") || 
                                       trimmedLine.Contains("+") || trimmedLine.Trim().EndsWith("0") || 
                                       trimmedLine.Trim().EndsWith("1") || trimmedLine.Trim().EndsWith("2") ||
                                       trimmedLine.Trim().EndsWith("3") || trimmedLine.Trim().EndsWith("4") ||
                                       trimmedLine.Trim().EndsWith("5") || trimmedLine.Trim().EndsWith("6") ||
                                       trimmedLine.Trim().EndsWith("7") || trimmedLine.Trim().EndsWith("8") ||
                                       trimmedLine.Trim().EndsWith("9")) &&
                                      !trimmedLine.Contains("x")) // Don't right-align lines with quantity (like [2x])
                             {
                                 x = e.PageBounds.Width - rightMargin - textSize.Width;
                             }
                             // Left align all other text (including main items) with proper margin
                             else
                             {
                                 x = leftMargin;
                             }
                            
                             // Override font size to 11 if we're in a table section (unless it's a separator line)
                             if (inTable11 && !trimmedLine.All(ch => ch == '-' || ch == '=' || ch == ' '))
                             {
                                 var currentSize = font.Size;
                                 if (currentSize != 11f)
                                 {
                                     font.Dispose();
                                     font = new Font("Arial", 9, font.Style);
                                 }
                             }
                             
                             // For table rows (with pipe separator), draw columns separately at fixed positions
                             if (inTable11 && trimmedLine.Contains("|") && !trimmedLine.All(ch => ch == '-' || ch == '=' || ch == ' '))
                             {
                                 var parts = trimmedLine.Split('|');
                                 if (parts.Length == 3)
                                 {
                                     var col1Text = parts[0].Trim();
                                     var col2Text = parts[1].Trim();
                                     var col3Text = parts[2].Trim();
                                     
                                     // Remove bold markers for measurement
                                     var col1Clean = col1Text.Replace("**", "");
                                     var col2Clean = col2Text.Replace("**", "");
                                     var col3Clean = col3Text.Replace("**", "");
                                     
                                     // Fixed column positions for Arial 11 font
                                     // Column 1: left-aligned at leftMargin
                                     float col1X = leftMargin;
                                     
                                     // Column 2: fixed position (after ~28 characters width)
                                     float col2X = leftMargin + (22 * 6.5f); // Approximate width for 22 chars in Arial 9
                                     
                                     // Column 3: right-aligned
                                     var col3Size = e.Graphics.MeasureString(col3Clean, font);
                                     float col3X = e.PageBounds.Width - rightMargin - col3Size.Width;
                                     
                                     // Draw each column
                                     e.Graphics.DrawString(col1Clean, font, brush, col1X, y);
                                     e.Graphics.DrawString(col2Clean, font, brush, col2X, y);
                                     e.Graphics.DrawString(col3Clean, font, brush, col3X, y);
                                     
                                    y += font.GetHeight() + 3;
                                    font.Dispose();
                                    
                                    // Check for page break before continuing
                                    // if (y > maxY)
                                    // {
                                    //     e.HasMorePages = true;
                                    //     currentLineIndex = i + 1;
                                    //     return;
                                    // }
                                    continue;
                                }
                            }
                            
                            e.Graphics.DrawString(trimmedLine, font, brush, x, y);
                            y += font.GetHeight() + 3;
                             
                             // Reset platform name section flag when we encounter order details
                             if (inPlatformNameSection && 
                                 (trimmedLine.Contains("Order placed") || 
                                  trimmedLine.Contains("Pickup -") || 
                                  trimmedLine.Contains("Delivery -") ||
                                  trimmedLine.Contains("TAKE AWAY") ||
                                  trimmedLine.Contains("DINE IN") ||
                                  trimmedLine.Contains("DELIVERY")))
                             {
                                 inPlatformNameSection = false;
                             }
                             
                             font.Dispose();
                             
                             // Check if we need a page break after drawing this line
                             // if (y > maxY)
                             // {
                             //     e.HasMorePages = true;
                             //     currentLineIndex = i + 1; // Save next line index for next page
                             //     return; // Exit to print next page
                             // }
                        }
                        
                        // All lines processed - no more pages
                        e.HasMorePages = false;
                        currentLineIndex = 0; // Reset for next print job
                        inTable11 = false; // Reset table flag for next print job
                    };

                    printDocument.Print();
                    printDocument.Dispose();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to print to {printerName}: {ex.Message}");
                }
            });
        }

        // Public wrapper to allow other components (e.g., Reports) to print arbitrary content
        public async Task PrintRawContentAsync(string printerName, string content)
        {
            await PrintToPrinterAsync(printerName, content);
        }
    }

    public static class StringExtensions
    {
        public static string PadCenter(this string text, int width)
        {
            if (text.Length >= width) return text;
            
            var padding = width - text.Length;
            var leftPadding = padding / 2;
            var rightPadding = padding - leftPadding;
            
            return text.PadLeft(text.Length + leftPadding).PadRight(width);
        }
    }
     
     public static class GraphicsExtensions
     {
         public static void DrawRoundedRectangle(this Graphics graphics, RectangleF rect, float radius, Pen pen)
         {
             var path = new System.Drawing.Drawing2D.GraphicsPath();
             
             // Create rounded rectangle path
             path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90);
             path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90);
             path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90);
             path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90);
             path.CloseFigure();
             
             graphics.DrawPath(pen, path);
             path.Dispose();
         }
     }
} 