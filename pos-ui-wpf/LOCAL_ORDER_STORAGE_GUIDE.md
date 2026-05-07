# Local Order Storage System

This document explains the local order storage system implemented for handling dine-in order modifications.

## Overview

The local order storage system creates and manages a `POS-Orders` folder on the user's desktop to store order data locally. This system handles complex dine-in order modifications with item status tracking (QUEUE → PREPARE → READY → SERVED) and modification rules based on item statuses.

## Implementation

### 1. Core Services

#### LocalOrderStorageService
- **Location**: `Services/LocalOrderStorageService.cs`
- **Purpose**: Manages file operations for dine-in orders
- **Features**: Save, load, update, delete order JSON files

#### DineInOrderService
- **Location**: `Services/DineInOrderService.cs`
- **Purpose**: Business logic for dine-in order management
- **Features**: Status management, modification rules, order lifecycle

### 2. Models

#### DineInOrderModel
- **Location**: `Models/DineInOrderModel.cs`
- **Purpose**: Represents a dine-in order with items and status tracking
- **Key Properties**: DisplayOrderId, Items, OrderStatus, TableNumber, etc.

#### DineInOrderItemModel
- **Purpose**: Represents individual items in a dine-in order
- **Key Properties**: ItemId, ItemName, Quantity, ItemStatus, IsNewItem, etc.

### 3. Folder Structure

```
Desktop/
└── POS-Orders/
    ├── 2024-01-15/          # Date-based subfolders
    │   ├── ORDER001.json    # Order files named by DisplayOrderId
    │   ├── ORDER002.json
    │   └── ORDER003.json
    ├── 2024-01-16/
    └── 2024-01-17/
```

### 4. Order Lifecycle

#### Order Creation
1. **Cart to Order**: When dine-in order is placed, cart items are converted to order items
2. **Initial Status**: All items start with `QUEUE` status
3. **File Creation**: Order is saved as JSON file named by DisplayOrderId

#### Status Progression
```
QUEUE → PREPARE → READY → SERVED
```

#### Modification Rules
- **QUEUE Status**: Can modify/remove items freely
- **Other Statuses**: Can only add new items, cannot remove existing items
- **New Items**: Always start with `QUEUE` status regardless of existing item statuses

#### Order Completion
- **File Deletion**: When order is completed, JSON file is deleted from local storage

## Usage Examples

### Creating a Dine-In Order
```csharp
var dineInService = DineInOrderService.Instance;

// Create order from current cart (automatically uses CartService.Instance)
bool created = await dineInService.CreateDineInOrderFromCartAsync("ORDER001");
```

### Loading and Modifying Orders
```csharp
// Load existing order
var order = await dineInService.LoadDineInOrderAsync("ORDER001");

// Check if order can be modified
bool canModify = await dineInService.CanModifyOrderAsync("ORDER001");

// Add current cart items to order
bool added = await dineInService.AddCartItemsToOrderAsync("ORDER001");

// Remove items (only if all items are in QUEUE status)
bool removed = await dineInService.RemoveItemsFromOrderAsync("ORDER001", itemIdsToRemove);

// Modify order with current cart items
bool modified = await dineInService.ModifyOrderWithCartAsync("ORDER001", itemsToRemove);
```

### Status Management
```csharp
// Update specific item status
bool updated = await dineInService.UpdateItemStatusAsync("ORDER001", itemId, "PREPARE");

// Move entire order to next status
bool moved = await dineInService.MoveOrderToNextStatusAsync("ORDER001");

// Get current order status
string status = dineInService.GetOrderCurrentStatus(order);
```

### File Operations
```csharp
var localStorage = LocalOrderStorageService.Instance;

// Save order to file
bool saved = await localStorage.SaveDineInOrderAsync(order);

// Load order from file
var loadedOrder = await localStorage.LoadDineInOrderAsync("ORDER001");

// Update existing order
bool updated = await localStorage.UpdateDineInOrderAsync(order);

// Delete order file (when completed)
bool deleted = await localStorage.DeleteDineInOrderAsync("ORDER001");

// Get all active orders
var activeOrders = await localStorage.GetAllActiveDineInOrdersAsync();
```

## Business Rules

### Modification Restrictions
1. **QUEUE Status**: Full modification allowed (add/remove/modify items)
2. **PREPARE Status**: Can only add new items, existing items cannot be removed
3. **READY Status**: Can only add new items, existing items cannot be removed
4. **SERVED Status**: Can only add new items, existing items cannot be removed

### Status Persistence
- **New Items**: Always start with `QUEUE` status
- **Existing Items**: Keep their current status when order is modified
- **Status Progression**: Items move through statuses sequentially

### File Management
- **Naming**: Files named by DisplayOrderId (e.g., `ORDER001.json`)
- **Location**: Stored in date-based folders (`yyyy-MM-dd`)
- **Cleanup**: Files deleted when order is completed

## Error Handling

- **Graceful Degradation**: Operations fail gracefully without crashing the application
- **Debug Logging**: Comprehensive logging for troubleshooting
- **Validation**: Business rule validation before operations
- **File Safety**: File operations wrapped in try-catch blocks

## Benefits

1. **Offline Capability**: Orders can be managed locally without internet connection
2. **Complex Modifications**: Handles complex dine-in order modifications efficiently
3. **Status Tracking**: Detailed item-level status tracking
4. **Business Rules**: Enforces modification rules based on item statuses
5. **Data Persistence**: Orders survive application restarts
6. **Organization**: Date-based folder structure for easy management
7. **Non-Intrusive**: Doesn't interfere with normal login process

## Integration Points

### Login Process
- **Automatic Initialization**: Folder created during login
- **No Interference**: Login continues even if folder creation fails

### Cart System
- **Seamless Conversion**: Cart items convert to order items with proper status
- **Modifier Support**: Handles item modifiers and customizations

### Order Management
- **Status Updates**: Integrates with kitchen display systems
- **Modification Workflow**: Supports order modification workflows

## Security Considerations

- **Local Storage**: Data is stored locally on the user's machine
- **No Network**: No sensitive data transmitted during file operations
- **User Permissions**: Respects user's desktop folder permissions
- **Error Handling**: Graceful handling of permission issues
- **Data Integrity**: JSON validation and error checking
