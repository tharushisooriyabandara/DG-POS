# Dine-In Order Storage Integration Example

This document shows how to integrate the dine-in order storage system with your existing cart and order placement workflow.

## Integration with Existing Order Placement

### 1. After Successful API Order Creation

When a dine-in order is successfully placed via API, add this code to save it locally:

```csharp
// In your order placement success handler
private async void OnOrderPlacedSuccessfully(string displayOrderId)
{
    try
    {
        // Your existing order placement logic...
        var apiResponse = await apiService.PlaceOrderAsync(orderRequest);
        
        if (apiResponse.Success)
        {
            // ✅ EXISTING: Your current success handling
            MessageBox.Show("Order placed successfully!");
            
            // ✅ NEW: Save order locally for dine-in modifications
            if (CartService.Instance.OrderType == "Dine In")
            {
                var dineInService = DineInOrderService.Instance;
                bool savedLocally = await dineInService.CreateDineInOrderFromCartAsync(displayOrderId);
                
                if (savedLocally)
                {
                    System.Diagnostics.Debug.WriteLine($"Dine-in order saved locally: {displayOrderId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save dine-in order locally: {displayOrderId}");
                }
            }
            
            // Clear cart and continue...
            CartService.Instance.ClearCart();
        }
    }
    catch (Exception ex)
    {
        // Your existing error handling...
    }
}
```

### 2. Order Modification Workflow (With Status-Based Restrictions)

When modifying an existing dine-in order, the system loads ALL items but enforces status-based restrictions:

```csharp
// Load existing order for modification (ALL items with status restrictions)
private async void ModifyExistingOrder(string displayOrderId)
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        
        // Get items that cannot be modified (PREPARE, READY, SERVED)
        var nonModifiableItems = await dineInService.GetNonModifiableItemsAsync(displayOrderId);
        
        if (nonModifiableItems.Count > 0)
        {
            var statuses = string.Join(", ", nonModifiableItems.Select(i => $"{i.ItemName} ({i.ItemStatus})"));
            MessageBox.Show($"Some items cannot be modified because they are already being prepared:\n{statuses}\n\nThese items will be displayed as read-only in the cart.");
        }
        
        // Load ALL items into cart (non-QUEUE items will be read-only)
        bool loaded = await dineInService.LoadOrderIntoCartForModificationAsync(displayOrderId);
        
        if (loaded)
        {
            // Navigate to cart modification page
            // Your existing navigation logic...
            
            // Show warning about read-only items if any
            if (nonModifiableItems.Count > 0)
            {
                MessageBox.Show($"Note: {nonModifiableItems.Count} items are read-only and cannot be modified.");
            }
        }
        else
        {
            MessageBox.Show("Failed to load order for modification.");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error loading order: {ex.Message}");
    }
}

// Check if a cart item is read-only (for UI binding)
private bool IsCartItemReadOnly(OrderItem cartItem)
{
    var dineInService = DineInOrderService.Instance;
    return dineInService.IsCartItemReadOnly(cartItem);
}

// Get the status of a cart item (for display)
private string GetCartItemStatus(OrderItem cartItem)
{
    var dineInService = DineInOrderService.Instance;
    return dineInService.GetCartItemStatus(cartItem);
}
```

// Save modifications back to local storage (preserves non-modifiable items)
private async void SaveOrderModifications(string displayOrderId)
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        
        // Save cart modifications, preserving non-modifiable items
        bool saved = await dineInService.SaveCartModificationsToOrderAsync(displayOrderId);
        
        if (saved)
        {
            MessageBox.Show("Order modifications saved successfully!");
            
            // Update the order via API if needed
            // Your existing API update logic...
        }
        else
        {
            MessageBox.Show("Failed to save order modifications.");
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error saving modifications: {ex.Message}");
    }
}

// Check what items can be modified before allowing modification
private async void CheckModificationEligibility(string displayOrderId)
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        
        var modifiableItems = await dineInService.GetModifiableItemsAsync(displayOrderId);
        var nonModifiableItems = await dineInService.GetNonModifiableItemsAsync(displayOrderId);
        
        if (modifiableItems.Count == 0)
        {
            MessageBox.Show("This order cannot be modified because all items are already being prepared or served.");
            return;
        }
        
        if (nonModifiableItems.Count > 0)
        {
            var message = $"Order can be partially modified.\n\n" +
                         $"Modifiable items ({modifiableItems.Count}):\n" +
                         string.Join("\n", modifiableItems.Select(i => $"• {i.ItemName}")) +
                         $"\n\nLocked items ({nonModifiableItems.Count}):\n" +
                         string.Join("\n", nonModifiableItems.Select(i => $"• {i.ItemName} ({i.ItemStatus})"));
            
            MessageBox.Show(message, "Modification Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("All items can be modified.", "Modification Status", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error checking modification eligibility: {ex.Message}");
    }
}
```

### 3. Kitchen Display Integration

For kitchen display systems, load orders from local storage:

```csharp
// Load all active dine-in orders for kitchen display
private async void LoadKitchenOrders()
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        var activeOrders = await dineInService.GetAllActiveOrdersAsync();
        
        // Group orders by status
        var queueOrders = activeOrders.Where(o => 
            dineInService.GetOrderCurrentStatus(o) == DineInOrderItemStatus.QUEUE).ToList();
            
        var preparingOrders = activeOrders.Where(o => 
            dineInService.GetOrderCurrentStatus(o) == DineInOrderItemStatus.PREPARE).ToList();
            
        var readyOrders = activeOrders.Where(o => 
            dineInService.GetOrderCurrentStatus(o) == DineInOrderItemStatus.READY).ToList();
        
        // Update your UI with these orders
        UpdateKitchenDisplay(queueOrders, preparingOrders, readyOrders);
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error loading kitchen orders: {ex.Message}");
    }
}

// Update order status (e.g., when kitchen starts preparing)
private async void UpdateOrderStatus(string displayOrderId, int itemId, string newStatus)
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        bool updated = await dineInService.UpdateItemStatusAsync(displayOrderId, itemId, newStatus);
        
        if (updated)
        {
            // Refresh kitchen display
            LoadKitchenOrders();
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error updating order status: {ex.Message}");
    }
}
```

### 4. Order Completion

When an order is completed:

```csharp
// Complete order and clean up local storage
private async void CompleteOrder(string displayOrderId)
{
    try
    {
        var dineInService = DineInOrderService.Instance;
        
        // Mark order as completed and delete local file
        bool completed = await dineInService.CompleteOrderAsync(displayOrderId);
        
        if (completed)
        {
            System.Diagnostics.Debug.WriteLine($"Order completed and cleaned up: {displayOrderId}");
            
            // Refresh kitchen display
            LoadKitchenOrders();
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Error completing order: {ex.Message}");
    }
}
```

## Key Integration Points

### 1. **Order Placement Success**
- ✅ Save dine-in orders locally after successful API placement
- ✅ Only for "Dine In" order type

### 2. **Order Modification**
- ✅ Load ALL items into cart with status-based restrictions
- ✅ QUEUE items: Fully modifiable (change quantity, remove, etc.)
- ✅ Non-QUEUE items: Read-only display (disabled, can't modify)
- ✅ All cart features work (discounts, notes, vouchers, etc.)
- ✅ Preserve non-modifiable items when saving modifications

### 3. **Kitchen Display**
- ✅ Load orders from local storage for kitchen display
- ✅ Update item statuses as kitchen progresses
- ✅ Real-time status tracking

### 4. **Order Completion**
- ✅ Clean up local files when orders are completed
- ✅ Automatic file deletion

## Benefits of This Integration

1. **Seamless Workflow**: Works with your existing cart and order placement system
2. **Offline Capability**: Orders can be managed locally without internet
3. **Status Tracking**: Detailed item-level status management
4. **Modification Rules**: Enforces business rules based on item statuses
5. **Kitchen Integration**: Real-time kitchen display updates
6. **Automatic Cleanup**: Files deleted when orders are completed

## Error Handling

The system includes comprehensive error handling:
- ✅ Graceful degradation if local storage fails
- ✅ Debug logging for troubleshooting
- ✅ Business rule validation
- ✅ File operation safety

This integration ensures that your dine-in orders are properly managed locally while maintaining compatibility with your existing cart and order placement workflow.
