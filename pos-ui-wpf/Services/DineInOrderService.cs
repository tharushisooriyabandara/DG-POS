using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class DineInOrderService
    {
        private static DineInOrderService _instance;
        private static readonly object _lock = new object();

        public static DineInOrderService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DineInOrderService();
                        }
                    }
                }
                return _instance;
            }
        }

        private DineInOrderService()
        {
        }

        #region Order Creation and Management

        /// <summary>
        /// Creates a new dine-in order from CartService and saves it locally
        /// </summary>
        public async Task<bool> CreateDineInOrderFromCartAsync(string displayOrderId)
        {
            try
            {
                var cartService = CartService.Instance;
                
                var order = new DineInOrderModel
                {
                    DisplayOrderId = displayOrderId,
                    CreatedAt = DateTime.Now,
                    LastModified = DateTime.Now,
                    OrderStatus = "ACTIVE",
                    TotalAmount = cartService.Total,
                    Notes = cartService.Note,
                    TableNumber = cartService.TableNumber?.ToString(),
                    CustomerName = cartService.CustomerName,
                    CustomerPhone = cartService.CustomerPhone,
                    Items = new List<DineInOrderItemModel>()
                };

                // Convert cart items to dine-in order items (prefer API item id if present)
                foreach (var cartItem in cartService.OrderItems)
                {
                    var orderItem = new DineInOrderItemModel
                    {
                        ItemId = cartItem.ApiItemId > 0 ? cartItem.ApiItemId : (cartItem.Product?.Id ?? 0),
                        ItemName = cartItem.Product?.ItemName ?? cartItem.Name,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price,
                        TotalPrice = cartItem.Total,
                        ItemStatus = DineInOrderItemStatus.QUEUE, // All new items start with QUEUE status
                        Notes = cartItem.Note,
                        ItemCreatedAt = DateTime.Now,
                        ItemLastModified = DateTime.Now,
                        IsNewItem = true,
                        Modifiers = new List<DineInOrderModifierModel>()
                    };

                    // Convert modifiers if any
                    if (cartItem.SelectedModifiers != null)
                    {
                        foreach (var modifierGroup in cartItem.SelectedModifiers)
                        {
                            foreach (var modifierName in modifierGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = modifierGroup.Key,
                                    ModifierName = modifierName,
                                    Price = 0, // You may need to get actual modifier price from your system
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    // Convert nested modifiers if any
                    if (cartItem.NestedModifierDetails != null)
                    {
                        foreach (var nestedGroup in cartItem.NestedModifierDetails)
                        {
                            foreach (var nestedModifier in nestedGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = 0, // You may need to get actual modifier ID
                                    ModifierName = $"{nestedGroup.Key}: {nestedModifier}",
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    order.Items.Add(orderItem);
                }

                // Save order to local storage
                var localStorage = LocalOrderStorageService.Instance;
                bool saved = await localStorage.SaveDineInOrderAsync(order);

                if (saved)
                {
                    System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Created dine-in order: {displayOrderId} with {order.Items.Count} items");
                }

                return saved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error creating dine-in order: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads a dine-in order from local storage
        /// </summary>
        public async Task<DineInOrderModel> LoadDineInOrderAsync(string displayOrderId)
        {
            try
            {
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.LoadDineInOrderAsync(displayOrderId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error loading dine-in order: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets all active dine-in orders
        /// </summary>
        public async Task<List<DineInOrderModel>> GetAllActiveOrdersAsync()
        {
            try
            {
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.GetAllActiveDineInOrdersAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error loading all active orders: {ex.Message}");
                return new List<DineInOrderModel>();
            }
        }

        #endregion

        #region Item Status Management

        /// <summary>
        /// Updates the status of a specific item in the order
        /// </summary>
        public async Task<bool> UpdateItemStatusAsync(string displayOrderId, int itemId, string newStatus)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                var item = order.Items.FirstOrDefault(i => i.ItemId == itemId);
                if (item == null)
                {
                    return false;
                }

                // Update item status
                item.ItemStatus = newStatus;
                item.ItemLastModified = DateTime.Now;

                // Update order
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.UpdateDineInOrderAsync(order);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error updating item status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates status of all items in an order to a specific status
        /// </summary>
        public async Task<bool> UpdateAllItemsStatusAsync(string displayOrderId, string newStatus)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    // Seed a brand new local order from current cart if none exists yet
                    var cart = CartService.Instance;
                    if (cart != null && cart.OrderItems != null && cart.OrderItems.Count > 0)
                    {
                        order = new DineInOrderModel
                        {
                            DisplayOrderId = displayOrderId,
                            CreatedAt = DateTime.Now,
                            LastModified = DateTime.Now,
                            OrderStatus = "ACTIVE",
                            Notes = cart.Note,
                            TableNumber = cart.TableNumber?.ToString(),
                            CustomerName = cart.CustomerName,
                            CustomerPhone = cart.CustomerPhone,
                            Items = cart.OrderItems.Select(ci => new DineInOrderItemModel
                            {
                                ItemId = ci.LocalItemId.HasValue && ci.LocalItemId.Value > 0 ? ci.LocalItemId.Value : 0,
                                ItemName = ci.Product?.ItemName ?? ci.Name,
                                Quantity = ci.Quantity,
                                UnitPrice = ci.Price,
                                TotalPrice = ci.Total,
                                ItemStatus = newStatus, // persist target status
                                Notes = ci.Note,
                                ItemCreatedAt = DateTime.Now,
                                ItemLastModified = DateTime.Now,
                                IsNewItem = false,
                                Modifiers = new List<DineInOrderModifierModel>()
                            }).ToList()
                        };

                        // Ensure unique incremental ItemIds
                        int nextId = order.Items.Any() ? 1 : 1;
                        foreach (var it in order.Items)
                        {
                            if (it.ItemId <= 0) it.ItemId = nextId++;
                        }

                        var localStorage = LocalOrderStorageService.Instance;
                        return await localStorage.SaveDineInOrderAsync(order);
                    }

                    // Nothing to seed from
                    return false;
                }

                bool anyUpdated = false;
                foreach (var item in order.Items)
                {
                    if (item.ItemStatus != newStatus)
                    {
                        item.ItemStatus = newStatus;
                        item.ItemLastModified = DateTime.Now;
                        anyUpdated = true;
                    }
                }

                if (anyUpdated)
                {
                    var localStorage = LocalOrderStorageService.Instance;
                    return await localStorage.UpdateDineInOrderAsync(order);
                }

                return true; // No changes needed
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error updating all items status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Ensures a local dine-in file exists and reflects the given OrderModel with a uniform per-item status.
        /// Used when Kitchen updates status but no local file exists yet.
        /// </summary>
        public async Task<bool> SeedOrUpdateFromOrderModelAsync(POS_UI.Models.OrderModel apiOrder, string localItemStatus)
        {
            try
            {
                if (apiOrder == null) return false;
                var displayId = apiOrder.OrderNumber ?? apiOrder.DisplayOrderId;
                if (string.IsNullOrWhiteSpace(displayId)) return false;

                var existing = await LoadDineInOrderAsync(displayId);
                if (existing == null)
                {
                    var model = new DineInOrderModel
                    {
                        DisplayOrderId = displayId,
                        CreatedAt = DateTime.Now,
                        LastModified = DateTime.Now,
                        OrderStatus = "ACTIVE",
                        Notes = apiOrder.OrderNotes,
                        TableNumber = apiOrder.TableNumber?.ToString(),
                        CustomerName = apiOrder.CustomerName,
                        CustomerPhone = apiOrder.CustomerPhone,
                        Items = new List<DineInOrderItemModel>()
                    };

                    int id = 1;
                    foreach (var it in (apiOrder.Items ?? new List<POS_UI.Models.OrderItem>()))
                    {
                        decimal unit = 0m;
                        if (it.ApiItemPrice > 0 && it.Quantity > 0)
                        {
                            unit = Math.Round(it.ApiItemPrice / it.Quantity, 2, MidpointRounding.AwayFromZero);
                        }
                        else if (it.Price > 0)
                        {
                            unit = it.Price;
                        }
                        else
                        {
                            unit = it.TotalPerItem;
                        }
                        model.Items.Add(new DineInOrderItemModel
                        {
                            ItemId = id++,
                            ItemName = it.Product?.ItemName ?? it.Name,
                            Quantity = it.Quantity > 0 ? it.Quantity : 1,
                            UnitPrice = unit,
                            TotalPrice = (unit > 0 ? unit * (it.Quantity > 0 ? it.Quantity : 1) : 0m),
                            ItemStatus = localItemStatus,
                            Notes = it.Note,
                            ItemCreatedAt = DateTime.Now,
                            ItemLastModified = DateTime.Now,
                            IsNewItem = false,
                            Modifiers = new List<DineInOrderModifierModel>()
                        });
                    }

                    var storage = LocalOrderStorageService.Instance;
                    return await storage.SaveDineInOrderAsync(model);
                }

                // If file exists, just update statuses
                return await UpdateAllItemsStatusAsync(displayId, localItemStatus);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Moves order to next status (QUEUE → PREPARE → READY → SERVED)
        /// </summary>
        public async Task<bool> MoveOrderToNextStatusAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                // Determine current status and next status
                var currentStatus = GetOrderCurrentStatus(order);
                string nextStatus = GetNextStatus(currentStatus);

                if (nextStatus == currentStatus)
                {
                    // Already at final status
                    return true;
                }

                // Update all items to next status
                return await UpdateAllItemsStatusAsync(displayOrderId, nextStatus);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error moving order to next status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current status of an order (based on item statuses)
        /// </summary>
        public string GetOrderCurrentStatus(DineInOrderModel order)
        {
            if (order?.Items == null || order.Items.Count == 0)
            {
                return DineInOrderItemStatus.QUEUE;
            }

            // If all items are SERVED, order is SERVED
            if (order.Items.All(i => i.ItemStatus == DineInOrderItemStatus.SERVED))
            {
                return DineInOrderItemStatus.SERVED;
            }

            // If any item is READY, order is READY
            if (order.Items.Any(i => i.ItemStatus == DineInOrderItemStatus.READY))
            {
                return DineInOrderItemStatus.READY;
            }

            // If any item is PREPARE, order is PREPARE
            if (order.Items.Any(i => i.ItemStatus == DineInOrderItemStatus.PREPARE))
            {
                return DineInOrderItemStatus.PREPARE;
            }

            // Default to QUEUE
            return DineInOrderItemStatus.QUEUE;
        }

        private string GetNextStatus(string currentStatus)
        {
            return currentStatus switch
            {
                DineInOrderItemStatus.QUEUE => DineInOrderItemStatus.PREPARE,
                DineInOrderItemStatus.PREPARE => DineInOrderItemStatus.READY,
                DineInOrderItemStatus.READY => DineInOrderItemStatus.SERVED,
                DineInOrderItemStatus.SERVED => DineInOrderItemStatus.SERVED, // Final status
                _ => DineInOrderItemStatus.QUEUE
            };
        }

        #endregion

        #region Order Modification

        /// <summary>
        /// Checks if an order can be modified (all items must be in QUEUE status)
        /// </summary>
        public async Task<bool> CanModifyOrderAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                // Check if all items are in QUEUE status
                return order.Items.All(i => i.ItemStatus == DineInOrderItemStatus.QUEUE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error checking if order can be modified: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if items can be removed from an order (all items must be in QUEUE status)
        /// </summary>
        public async Task<bool> CanRemoveItemsAsync(string displayOrderId)
        {
            return await CanModifyOrderAsync(displayOrderId);
        }

        /// <summary>
        /// Adds current cart items to an existing order
        /// </summary>
        public async Task<bool> AddCartItemsToOrderAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                var cartService = CartService.Instance;

                // Add new items with QUEUE status (skip read-only items that reflect past PREPARE/READY/SERVED)
                int nextItemId = order.Items.Any() ? order.Items.Max(i => i.ItemId) + 1 : 1;
                foreach (var cartItem in cartService.OrderItems)
                {
                    if ((cartItem.Note != null && cartItem.Note.Contains("[STATUS:")) || cartItem.IsReadOnly)
                    {
                        // This is a past item; keep as-is (already preserved above) and do not add a new QUEUE copy
                        continue;
                    }
                    var orderItem = new DineInOrderItemModel
                    {
                        ItemId = cartItem.ApiItemId > 0 ? cartItem.ApiItemId : nextItemId++,
                        ItemName = cartItem.Product?.ItemName ?? cartItem.Name,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price,
                        TotalPrice = cartItem.Total,
                        ItemStatus = DineInOrderItemStatus.QUEUE, // New items always start with QUEUE
                        Notes = cartItem.Note,
                        ItemCreatedAt = DateTime.Now,
                        ItemLastModified = DateTime.Now,
                        IsNewItem = true,
                        Modifiers = new List<DineInOrderModifierModel>()
                    };

                    // Convert modifiers if any
                    if (cartItem.SelectedModifiers != null)
                    {
                        foreach (var modifierGroup in cartItem.SelectedModifiers)
                        {
                            foreach (var modifierName in modifierGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = modifierGroup.Key,
                                    ModifierName = modifierName,
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    // Convert nested modifiers if any
                    if (cartItem.NestedModifierDetails != null)
                    {
                        foreach (var nestedGroup in cartItem.NestedModifierDetails)
                        {
                            foreach (var nestedModifier in nestedGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = 0, // You may need to get actual modifier ID
                                    ModifierName = $"{nestedGroup.Key}: {nestedModifier}",
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    order.Items.Add(orderItem);
                }

                // Recalculate total
                order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
                order.LastModified = DateTime.Now;

                // Save updated order
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.UpdateDineInOrderAsync(order);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error adding items to order: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes items from an order (only if all items are in QUEUE status)
        /// </summary>
        public async Task<bool> RemoveItemsFromOrderAsync(string displayOrderId, List<int> itemIdsToRemove)
        {
            try
            {
                // Check if order can be modified
                if (!await CanRemoveItemsAsync(displayOrderId))
                {
                    System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Cannot remove items: Order {displayOrderId} has items not in QUEUE status");
                    return false;
                }

                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                // Remove specified items
                order.Items.RemoveAll(i => itemIdsToRemove.Contains(i.ItemId));

                // Recalculate total
                order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
                order.LastModified = DateTime.Now;

                // Save updated order
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.UpdateDineInOrderAsync(order);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error removing items from order: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Modifies an existing order with current cart items (adds new items, keeps existing item statuses)
        /// </summary>
        public async Task<bool> ModifyOrderWithCartAsync(string displayOrderId, List<int> itemsToRemove = null)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                // Mark existing items as not new (so they keep their current status)
                foreach (var item in order.Items)
                {
                    item.IsNewItem = false;
                }

                // Remove items if specified and allowed
                if (itemsToRemove != null && itemsToRemove.Count > 0)
                {
                    if (!await CanRemoveItemsAsync(displayOrderId))
                    {
                        System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Cannot remove items: Order {displayOrderId} has items not in QUEUE status");
                        return false;
                    }
                    order.Items.RemoveAll(i => itemsToRemove.Contains(i.ItemId));
                }

                var cartService = CartService.Instance;

                // Add new items with QUEUE status
                int nextItemId = order.Items.Any() ? order.Items.Max(i => i.ItemId) + 1 : 1;
                foreach (var cartItem in cartService.OrderItems)
                {
                    var orderItem = new DineInOrderItemModel
                    {
                        ItemId = nextItemId++,
                        ItemName = cartItem.Name,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price,
                        TotalPrice = cartItem.Total,
                        ItemStatus = DineInOrderItemStatus.QUEUE, // New items always start with QUEUE
                        Notes = cartItem.Note,
                        ItemCreatedAt = DateTime.Now,
                        ItemLastModified = DateTime.Now,
                        IsNewItem = true,
                        Modifiers = new List<DineInOrderModifierModel>()
                    };

                    // Convert modifiers if any
                    if (cartItem.SelectedModifiers != null)
                    {
                        foreach (var modifierGroup in cartItem.SelectedModifiers)
                        {
                            foreach (var modifierName in modifierGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = modifierGroup.Key,
                                    ModifierName = modifierName,
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    // Convert nested modifiers if any
                    if (cartItem.NestedModifierDetails != null)
                    {
                        foreach (var nestedGroup in cartItem.NestedModifierDetails)
                        {
                            foreach (var nestedModifier in nestedGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = 0, // You may need to get actual modifier ID
                                    ModifierName = $"{nestedGroup.Key}: {nestedModifier}",
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    order.Items.Add(orderItem);
                }

                // Recalculate total
                order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
                order.LastModified = DateTime.Now;

                // Save updated order
                var localStorage = LocalOrderStorageService.Instance;
                return await localStorage.UpdateDineInOrderAsync(order);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error modifying order: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Cart Integration for Modifications

        /// <summary>
        /// Loads a dine-in order into cart for modification with status-based restrictions
        /// </summary>
        public async Task<bool> LoadOrderIntoCartForModificationAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                var cartService = CartService.Instance;

                // NEW: Do not replace cart items to preserve original item IDs used by the UI to lock edits.
                // Instead, mark matching existing cart items as read-only based on local per-item statuses.
                var existingCartItems = cartService.OrderItems.ToList();

                // Improve matching: do not reuse the same local item for multiple cart lines
                var localItemsPool = order.Items.ToList();
                var usedLocalIds = new HashSet<int>();
                foreach (var cartItem in existingCartItems)
                {
                    var cartName = cartItem.Product?.ItemName ?? cartItem.Name ?? string.Empty;
                    var cartQty = cartItem.Quantity;
                    var cartUnit = Math.Round(cartItem.Price, 2, MidpointRounding.AwayFromZero);

                    // Candidate set: same name and not already used
                    var candidates = localItemsPool
                        .Where(i => !usedLocalIds.Contains(i.ItemId)
                            && string.Equals(i.ItemName ?? string.Empty, cartName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    // Prefer non-QUEUE candidates first
                    DineInOrderItemModel match = candidates
                        .Where(i => i.ItemStatus != DineInOrderItemStatus.QUEUE)
                        .OrderByDescending(i => Math.Round(i.UnitPrice, 2, MidpointRounding.AwayFromZero) == cartUnit ? 1 : 0)
                        .ThenByDescending(i => i.Quantity == cartQty ? 1 : 0)
                        .FirstOrDefault();

                    // If none, fallback to QUEUE candidates
                    if (match == null)
                    {
                        match = candidates
                            .OrderByDescending(i => Math.Round(i.UnitPrice, 2, MidpointRounding.AwayFromZero) == cartUnit ? 1 : 0)
                            .ThenByDescending(i => i.Quantity == cartQty ? 1 : 0)
                            .FirstOrDefault();
                    }

                    if (match != null)
                    {
                        usedLocalIds.Add(match.ItemId);
                        cartItem.LocalItemId = match.ItemId;

                        if (match.ItemStatus != DineInOrderItemStatus.QUEUE)
                    {
                        cartItem.IsReadOnly = true;
                            cartItem.OriginalStatus = match.ItemStatus;
                        }
                        else
                        {
                            // Ensure editable for QUEUE
                            cartItem.IsReadOnly = false;
                            cartItem.OriginalStatus = DineInOrderItemStatus.QUEUE;
                        }
                    }
                    else
                    {
                        // No match in local file. If order currently in a kitchen-locked status, default old items to read-only
                        // We cannot reliably disambiguate, so leave QUEUE items editable and lock others conservatively.
                        if (cartItem.OriginalStatus != DineInOrderItemStatus.QUEUE && !string.IsNullOrWhiteSpace(cartItem.OriginalStatus))
                        {
                            cartItem.IsReadOnly = true;
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Applied local status overlay to {existingCartItems.Count} cart items for order {displayOrderId}.");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error loading order into cart for modification: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets items that cannot be modified (PREPARE, READY, SERVED status)
        /// </summary>
        public async Task<List<DineInOrderItemModel>> GetNonModifiableItemsAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return new List<DineInOrderItemModel>();
                }

                return order.Items.Where(i => 
                    i.ItemStatus != DineInOrderItemStatus.QUEUE).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error getting non-modifiable items: {ex.Message}");
                return new List<DineInOrderItemModel>();
            }
        }

        /// <summary>
        /// Gets items that can be modified (QUEUE status only)
        /// </summary>
        public async Task<List<DineInOrderItemModel>> GetModifiableItemsAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return new List<DineInOrderItemModel>();
                }

                return order.Items.Where(i => 
                    i.ItemStatus == DineInOrderItemStatus.QUEUE).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error getting modifiable items: {ex.Message}");
                return new List<DineInOrderItemModel>();
            }
        }

        /// <summary>
        /// Saves cart modifications back to local storage, preserving non-modifiable items
        /// </summary>
        public async Task<bool> SaveCartModificationsToOrderAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                var cartService = CartService.Instance;

                // Build a map of read-only items from the current cart by local identity
                var readOnlyLocalIds = new HashSet<int>(cartService.OrderItems
                    .Where(ci => ci.IsReadOnly && ci.LocalItemId.HasValue)
                    .Select(ci => ci.LocalItemId.Value));

                // Preserve ALL existing items to avoid accidental loss when local status is stale
                var preservedItems = order.Items.ToList();
                // Ensure preserved items are flagged as not-new
                foreach (var pi in preservedItems)
                {
                    pi.IsNewItem = false;
                }

                // Seed preserved items from current cart if file lacks them but UI marked items read-only (kitchen-locked load)
                // This handles the first transition when the local file hasn't been created/updated yet.
                var existingIds = new HashSet<int>(preservedItems.Select(p => p.ItemId));
                var existingKeys = new HashSet<string>(preservedItems.Select(p => $"{p.ItemName}|{Math.Round(p.UnitPrice, 2, MidpointRounding.AwayFromZero):0.00}|{(p.Notes ?? string.Empty).Trim()}"));
                foreach (var ci in cartService.OrderItems.Where(x => x.IsReadOnly))
                {
                    var key = $"{(ci.Product?.ItemName ?? ci.Name) ?? string.Empty}|{Math.Round(ci.Price, 2, MidpointRounding.AwayFromZero):0.00}|{(ci.Note ?? string.Empty).Trim()}";
                    if (ci.LocalItemId.HasValue && existingIds.Contains(ci.LocalItemId.Value)) continue;
                    if (existingKeys.Contains(key)) continue;

                    var status = !string.IsNullOrWhiteSpace(ci.OriginalStatus) ? ci.OriginalStatus : DineInOrderItemStatus.PREPARE;
                    var seed = new DineInOrderItemModel
                    {
                        ItemId = ci.LocalItemId.HasValue && ci.LocalItemId.Value > 0 ? ci.LocalItemId.Value : (preservedItems.Any() ? preservedItems.Max(i => i.ItemId) + 1 : 1),
                        ItemName = ci.Product?.ItemName ?? ci.Name,
                        Quantity = ci.Quantity,
                        UnitPrice = ci.Price,
                        TotalPrice = ci.Total,
                        ItemStatus = status,
                        Notes = ci.Note,
                        ItemCreatedAt = DateTime.Now,
                        ItemLastModified = DateTime.Now,
                        IsNewItem = false,
                        Modifiers = new List<DineInOrderModifierModel>()
                    };
                    preservedItems.Add(seed);
                    existingIds.Add(seed.ItemId);
                    existingKeys.Add(key);
                }

                // Build a comparison set of preserved items (kept for reference; do not filter new items by this)
                var preservedKeys = new HashSet<string>(preservedItems.Select(i =>
                    $"{i.ItemId}|{i.ItemName}|{i.UnitPrice:0.####}|{(i.Notes ?? string.Empty).Trim()}"));

                // Start from existing items and only append new QUEUE lines
                var updatedItems = preservedItems
                    .Select(i => new DineInOrderItemModel
                    {
                        ItemId = i.ItemId,
                        ItemName = i.ItemName,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice,
                        TotalPrice = i.TotalPrice,
                        ItemStatus = i.ItemStatus, // preserve existing status PREPARE/READY/SERVED/QUEUE
                        Notes = i.Notes,
                        Modifiers = i.Modifiers?.Select(m => new DineInOrderModifierModel
                        {
                            ModifierId = m.ModifierId,
                            ModifierName = m.ModifierName,
                            Price = m.Price,
                            Quantity = m.Quantity
                        }).ToList() ?? new List<DineInOrderModifierModel>(),
                        ItemCreatedAt = i.ItemCreatedAt,
                        ItemLastModified = i.ItemLastModified,
                        IsNewItem = false
                    })
                    .ToList();

                // Compute next unique ItemId for any new items (avoid collisions with preserved items)
                int nextItemId = order.Items.Any() ? order.Items.Max(i => i.ItemId) + 1 : 1;

                // Add current cart items as new QUEUE items (skip read-only/past items)
                foreach (var cartItem in cartService.OrderItems)
                {
                    // Skip items that were originally non-QUEUE (preserved) or marked read-only
                    if ((cartItem.Note != null && cartItem.Note.Contains("[STATUS:")) || cartItem.IsReadOnly)
                    {
                        continue; // Skip this item as it's a preserved non-QUEUE item
                    }

                    // Skip if an identical QUEUE line already exists in preserved (avoid duplicates)
                    bool identicalExists = updatedItems.Any(pi =>
                        string.Equals(pi.ItemName ?? string.Empty, (cartItem.Product?.ItemName ?? cartItem.Name) ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && Math.Round(pi.UnitPrice, 2, MidpointRounding.AwayFromZero) == Math.Round(cartItem.Price, 2, MidpointRounding.AwayFromZero)
                        && (pi.Notes ?? string.Empty).Trim() == (cartItem.Note ?? string.Empty).Trim()
                        && pi.ItemStatus == DineInOrderItemStatus.QUEUE);
                    if (identicalExists)
                    {
                        continue;
                    }

                    var orderItem = new DineInOrderItemModel
                    {
                        ItemId = nextItemId++,
                        ItemName = cartItem.Product?.ItemName ?? cartItem.Name,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Price,
                        TotalPrice = cartItem.Total,
                        ItemStatus = DineInOrderItemStatus.QUEUE, // New/modified items always start with QUEUE
                        Notes = cartItem.Note,
                        ItemCreatedAt = DateTime.Now,
                        ItemLastModified = DateTime.Now,
                        IsNewItem = true,
                        Modifiers = new List<DineInOrderModifierModel>()
                    };

                    // Convert modifiers if any
                    if (cartItem.SelectedModifiers != null)
                    {
                        foreach (var modifierGroup in cartItem.SelectedModifiers)
                        {
                            foreach (var modifierName in modifierGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = modifierGroup.Key,
                                    ModifierName = modifierName,
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    // Convert nested modifiers if any
                    if (cartItem.NestedModifierDetails != null)
                    {
                        foreach (var nestedGroup in cartItem.NestedModifierDetails)
                        {
                            foreach (var nestedModifier in nestedGroup.Value)
                            {
                                orderItem.Modifiers.Add(new DineInOrderModifierModel
                                {
                                    ModifierId = 0, // You may need to get actual modifier ID
                                    ModifierName = $"{nestedGroup.Key}: {nestedModifier}",
                                    Price = 0, // You may need to get actual modifier price
                                    Quantity = 1
                                });
                            }
                        }
                    }

                    updatedItems.Add(orderItem);
                }

                // Store updated items back on order
                order.Items = updatedItems;

                // Update order-level properties from cart
                order.Notes = cartService.Note;
                order.CustomerName = cartService.CustomerName;
                order.CustomerPhone = cartService.CustomerPhone;
                order.TableNumber = cartService.TableNumber?.ToString();

                // Recalculate total
                order.TotalAmount = order.Items.Sum(i => i.TotalPrice);
                order.LastModified = DateTime.Now;

                // Save updated order
                var localStorage = LocalOrderStorageService.Instance;
                bool saved = await localStorage.UpdateDineInOrderAsync(order);

                if (saved)
                {
                    System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Saved cart modifications to order: {displayOrderId}. Preserved {preservedItems.Count} non-modifiable items.");
                }

                return saved;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error saving cart modifications to order: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a cart item is read-only (non-QUEUE status)
        /// </summary>
        public bool IsCartItemReadOnly(OrderItem cartItem)
        {
            return cartItem.Note != null && cartItem.Note.Contains("[STATUS:");
        }

        /// <summary>
        /// Gets the status of a cart item (extracts from note if it's a read-only item)
        /// </summary>
        public string GetCartItemStatus(OrderItem cartItem)
        {
            if (cartItem.Note != null && cartItem.Note.Contains("[STATUS:"))
            {
                var statusMatch = System.Text.RegularExpressions.Regex.Match(cartItem.Note, @"\[STATUS: (\w+)\]");
                if (statusMatch.Success)
                {
                    return statusMatch.Groups[1].Value;
                }
            }
            return DineInOrderItemStatus.QUEUE; // Default to QUEUE for modifiable items
        }

        #endregion

        #region Order Completion

        /// <summary>
        /// Completes an order (marks as completed and deletes local file)
        /// </summary>
        public async Task<bool> CompleteOrderAsync(string displayOrderId)
        {
            try
            {
                var order = await LoadDineInOrderAsync(displayOrderId);
                if (order == null)
                {
                    return false;
                }

                // Mark order as completed
                order.OrderStatus = "COMPLETED";
                order.LastModified = DateTime.Now;

                // Update order first
                var localStorage = LocalOrderStorageService.Instance;
                bool updated = await localStorage.UpdateDineInOrderAsync(order);

                if (updated)
                {
                    // Delete the file
                    bool deleted = await localStorage.DeleteDineInOrderAsync(displayOrderId);
                    if (deleted)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Completed and deleted order: {displayOrderId}");
                    }
                    return deleted;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DineInOrderService] Error completing order: {ex.Message}");
                return false;
            }
        }

        #endregion
    }
}
