# Discount Mode Implementation Guide

## Overview
This guide explains the implementation of the `discount_mode_applied` field in order APIs to track whether discounts are applied as percentages or fixed amounts.

## New Field: `discount_mode_applied`

### Purpose
The `discount_mode_applied` field identifies how the discount is applied to an order:
- **"percentage"**: Discount is applied as a percentage of the total
- **"value"**: Discount is applied as a fixed amount

### Implementation Details

#### 1. OrderModel Updates
- **Added Property**: `DiscountModeApplied { get; set; } = "percentage"`
- **Default Value**: "percentage" (backward compatibility)
- **API Integration**: Included in `ToApiRequest()` method

#### 2. API Request Structure
```json
{
  "discount": 5.00,  // Contains the actual discount amount/value
  "discount_percentage": 10.0,  // Contains the percentage value
  "discount_mode_applied": "percentage",  // "percentage" or "value"
  "discount_type": "",
  // ... other fields
}
```

#### 3. API Response Parsing
- **GetOrderByIdAsync**: Parses `discount` and `discount_mode_applied` from API response
- **ParseOrderFromTable**: Parses `discount` and `discount_mode_applied` from order list
- **Logic**: Uses `discount_mode_applied` to determine whether `discount` value is percentage or amount
- **Default Fallback**: "percentage" if field is missing

#### 4. Order Creation Logic
```csharp
// In OrderModel.FromCartService()
string discountMode = discountPercentage > 0 ? "percentage" : "value";

// In ToApiRequest()
discount = DiscountAmount,  // Always send the actual discount amount
discount_percentage = DiscountPercentage  // Always send the percentage value
```

### Usage Examples

#### Percentage Discount
```csharp
var order = new OrderModel
{
    DiscountAmount = 5.00,  // Calculated discount amount
    DiscountPercentage = 10.0,
    DiscountModeApplied = "percentage"
};
// API Request: { "discount": 5.00, "discount_percentage": 10.0, "discount_mode_applied": "percentage" }
```

#### Fixed Amount Discount
```csharp
var order = new OrderModel
{
    DiscountAmount = 5.00,
    DiscountPercentage = 0.0,
    DiscountModeApplied = "value"
};
// API Request: { "discount": 5.00, "discount_percentage": 0.0, "discount_mode_applied": "value" }
```

### Backward Compatibility

#### Existing Orders
- Orders without `discount_mode_applied` field default to "percentage"
- No breaking changes to existing functionality

#### API Integration
- Field is optional in API requests
- Missing field defaults to "percentage" in responses

### Affected Components

#### 1. Models
- **OrderModel**: Added `DiscountModeApplied` property
- **DraftOrderModel**: Added `DiscountModeApplied` property

#### 2. Services
- **ApiService**: Updated parsing methods
- **DraftStorageService**: Automatic JSON serialization support

#### 3. ViewModels
- **CashierHomeViewModel**: Updated draft creation/loading logic

### API Endpoints Updated

#### Create Order API
- **Field**: `discount_mode_applied`
- **Type**: string
- **Values**: "percentage" | "value"

#### Update Order API
- **Field**: `discount_mode_applied`
- **Type**: string
- **Values**: "percentage" | "value"

#### Get Order API
- **Response**: Includes `discount_mode_applied` field
- **Default**: "percentage" if not present

#### Get Orders List API
- **Response**: Includes `discount_mode_applied` field
- **Default**: "percentage" if not present

### Business Logic

#### Discount Calculation
1. **Percentage Mode**: `DiscountAmount = Total * (discount / 100)` where `discount` contains percentage value
2. **Value Mode**: `DiscountAmount = discount` where `discount` contains fixed amount value

#### UI Display
- **Percentage**: Shows "Discount (10%)"
- **Amount**: Shows "Discount ($5.00)"

### Testing Scenarios

#### 1. Create Order with Percentage Discount
```csharp
// Should set discount_mode_applied = "percentage"
var order = OrderModel.FromCartService(cartService, "ORDER001", customer, 10.0m);
```

#### 2. Create Order with Fixed Amount Discount
```csharp
// Should set discount_mode_applied = "value"
var order = OrderModel.FromCartService(cartService, "ORDER001", customer, 0.0m);
order.DiscountAmount = 5.00m;
// API Request: { "discount": 5.00, "discount_mode_applied": "value" }
```

#### 3. Load Existing Order
```csharp
// Should parse discount_percentage, discount, and discount_mode_applied from API response
var order = await apiService.GetOrderByIdAsync(orderId);
// If API returns: { "discount": 5.00, "discount_percentage": 10.0, "discount_mode_applied": "percentage" }
// Then: order.DiscountPercentage = 10.0, order.DiscountAmount = 5.00
// If API returns: { "discount": 5.00, "discount_percentage": 0.0, "discount_mode_applied": "value" }
// Then: order.DiscountAmount = 5.00, order.DiscountPercentage = 0.0
```

### Future Enhancements

#### 1. UI Improvements
- Add discount mode selection in order creation
- Display discount mode in order details

#### 2. Validation
- Ensure discount_mode_applied matches discount calculation
- Validate percentage range (0-100)

#### 3. Reporting
- Include discount mode in order reports
- Track discount mode usage statistics
