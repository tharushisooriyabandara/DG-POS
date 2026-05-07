# Receipt Printing Guide

## Overview

The POS system now includes enhanced receipt printing functionality that can print beautifully formatted receipts with all cart information including items, modifiers, totals, discounts, and shop details.

## New Cart Receipt Printing

### Method Signature
```csharp
public async Task PrintCartReceiptAsync(CartService cartService, string orderNumber = null, CardTransactionResult cardTransaction = null)
```

### Usage Examples

#### 1. Print Cart Receipt (Basic)
```csharp
// Get the cart service instance
var cartService = CartService.Instance;

// Print the current cart
await ReceiptPrintingService.Instance.PrintCartReceiptAsync(cartService);
```

#### 2. Print Cart Receipt with Order Number
```csharp
var cartService = CartService.Instance;
var orderNumber = "ORD-2024-001";

await ReceiptPrintingService.Instance.PrintCartReceiptAsync(cartService, orderNumber);
```

#### 3. Print Cart Receipt with Card Transaction
```csharp
var cartService = CartService.Instance;
var orderNumber = "ORD-2024-001";
var cardTransaction = new CardTransactionResult 
{
    CardPan = "****1234",
    CardScheme = "VISA",
    AuthorizationCode = "AUTH123"
};

await ReceiptPrintingService.Instance.PrintCartReceiptAsync(cartService, orderNumber, cardTransaction);
```

#### 4. Print After Order Completion
```csharp
// In your order completion method
private async void CompleteOrder()
{
    var cartService = CartService.Instance;
    var orderNumber = GenerateOrderNumber(); // Your order number generation logic
    CardTransactionResult cardTransaction = null; // Set if card payment was used
    
    // Process payment and get card transaction if applicable
    if (SelectedPaymentMethod == PaymentMethod.Card)
    {
        cardTransaction = await ProcessCardPayment();
    }
    
    // Print receipt
    await ReceiptPrintingService.Instance.PrintCartReceiptAsync(cartService, orderNumber, cardTransaction);
    
    // Clear cart after printing
    cartService.ClearCart();
}
```

## Receipt Content

The new cart receipt includes the following sections:

### 1. Header Section
- **Restaurant Name** (from shop details)
- **Shop Details** (address, phone, email)

### 2. Order Information
- **Order Number** (auto-generated if not provided)
- **Date and Time**
- **Order Type** (Take Away, Dine In, Delivery)

### 3. Customer Information
- **Customer Name**
- **Phone Number**
- **Table Number** (for dine-in orders)

### 4. Cashier Information
- **Cashier Name** (from current user details)

### 5. Items Ordered
- **Item Name** with quantity
- **Size** (if applicable)
- **Modifiers** with prices
- **Item Notes** (if any)

### 6. Order Summary
- **Subtotal**
- **Discount** (with description)
- **Coupon** (with code and description)
- **Delivery Charge** (if applicable)
- **Total**

### 7. Payment Method
- **Card Payment** (with card details if applicable)
- **Cash Payment**

### 8. Footer
- **Thank you message**
- **Shop contact information**
- **Receipt retention notice**

## Integration with Existing Code

### In CashierHomeViewModel.cs
You can add receipt printing to your order completion flow:

```csharp
private async void ConfirmOrder()
{
    try
    {
        // Your existing order processing logic
        
        // Generate order number
        var orderNumber = $"ORD-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}";
        
        // Print receipt
        await ReceiptPrintingService.Instance.PrintCartReceiptAsync(
            _cartService, 
            orderNumber, 
            _lastCardTransactionResult
        );
        
        // Clear cart
        _cartService.ClearCart();
        
        // Show success message
        MessageBox.Show("Order completed and receipt printed successfully!");
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error completing order: {ex.Message}");
    }
}
```

### In CheckoutDialog.xaml.cs
You can add receipt printing to the checkout process:

```csharp
private async void ProcessPayment()
{
    try
    {
        // Your payment processing logic
        
        // Print receipt
        await ReceiptPrintingService.Instance.PrintCartReceiptAsync(
            CartService.Instance,
            orderNumber,
            cardTransactionResult
        );
        
        // Close dialog
        DialogResult = true;
        Close();
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Error processing payment: {ex.Message}");
    }
}
```

## Features

### 1. Dynamic Shop Information
- Uses `GlobalDataService.Instance.ShopDetails` for shop information
- Automatically displays shop name, address, phone, and email
- Uses shop currency from settings

### 2. Complete Item Details
- Shows all items with quantities and prices
- Displays modifiers with their prices
- Shows item sizes (if not "Default")
- Includes item notes

### 3. Comprehensive Pricing
- Subtotal calculation
- Discount display with descriptions
- Coupon information with codes
- Delivery charges
- Final total

### 4. Payment Information
- Supports both cash and card payments
- Shows card details for card transactions
- Displays authorization codes

### 5. Professional Formatting
- Centered headers and important information
- Right-aligned prices
- Proper spacing and separators
- Different font sizes for different content types

### 6. Multi-Printer Support
- Automatically detects active printers
- Prints to all active printers simultaneously
- Error handling for individual printer failures

## Error Handling

The receipt printing service includes comprehensive error handling:

```csharp
try
{
    await ReceiptPrintingService.Instance.PrintCartReceiptAsync(cartService, orderNumber);
}
catch (Exception ex)
{
    // Handle printing errors
    MessageBox.Show($"Failed to print receipt: {ex.Message}");
    // You might want to log this error or show a user-friendly message
}
```

## Printer Configuration

The service uses the existing `PrintersService` to get active printers:

```csharp
// The service automatically gets active printers from PrintersService
var activePrinters = ReceiptPrintingService.Instance.GetActivePrinters();
```

## Customization

You can customize the receipt format by modifying the `GenerateCartReceiptContent` method in `ReceiptPrintingService.cs`. The method is well-structured and easy to modify for different receipt layouts.

## Best Practices

1. **Always provide an order number** for better tracking
2. **Handle printing errors gracefully** - don't let printing failures stop order processing
3. **Print receipts after successful payment** to ensure order completion
4. **Clear the cart after printing** to prepare for the next order
5. **Use appropriate error messages** for different failure scenarios

## Troubleshooting

### Common Issues

1. **No printers found**
   - Check if printers are configured in PrintersService
   - Verify printer drivers are installed
   - Ensure printers are set as active

2. **Printing fails**
   - Check printer connectivity
   - Verify printer has paper
   - Check printer status (online/offline)

3. **Receipt format issues**
   - Check if shop details are loaded in GlobalDataService
   - Verify cart has items before printing
   - Ensure all required services are initialized

### Debug Information

The service includes debug logging that can help troubleshoot issues:

```csharp
// Enable debug output to see printer detection and printing status
System.Diagnostics.Debug.WriteLine("Printing receipt...");
```

This enhanced receipt printing system provides a professional, comprehensive receipt that includes all the information customers expect while maintaining the flexibility to work with your existing order processing workflow. 