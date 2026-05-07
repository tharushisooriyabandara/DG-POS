# Printer Settings Guide

## Overview
This guide explains the printer settings system that allows you to configure individual printers for different types of receipts (main receipts and kitchen receipts) with local storage persistence.

## Features

### Printer Settings Model
The `PrinterSettingsModel` contains the following properties:
- **DeviceName**: Printer name (used as unique identifier)
- **ConnectedVia**: Connection type (USB, Network, etc.)
- **IsActive**: Whether the printer is active/inactive
- **MainReceipt**: Enable/disable main receipt printing
- **KitchenReceipt**: Enable/disable kitchen receipt printing
- **MainReceiptCount**: Number of copies for main receipts
- **KitchenReceiptCount**: Number of copies for kitchen receipts
- **PaperSize**: Paper size setting (default: "80mm")
- **PrintWidth**: Print width in characters (default: 32)
- **AutoCut**: Auto-cut setting (default: true)
- **FontSize**: Font size setting (default: "Normal")
- **BoldHeader**: Bold header setting (default: true)
- **ShowLogo**: Show logo setting (default: false)
- **CustomSettings**: Custom settings string

### Local Storage
- **File Location**: `Desktop/printers.txt`
- **Format**: JSON array of `PrinterSettingsModel` objects
- **Persistence**: Settings are automatically saved when modified
- **Loading**: Settings are loaded when the application starts

## How It Works

### 1. Printer Detection
When the application starts, `PrintersService` detects POS printers and:
- Checks if settings exist for each printer in `printers.txt`
- If settings exist, loads them and sets `IsActive` accordingly
- If no settings exist, creates default settings and saves them

### 2. Settings Management
- **PrinterSettingsService**: Singleton service managing local storage
- **AddPrinterSettings()**: Adds new printer settings
- **UpdatePrinterSettings()**: Updates existing settings
- **GetPrinterSettings()**: Retrieves settings for a specific printer
- **HasPrinterSettings()**: Checks if settings exist for a printer

### 3. Receipt Printing Logic

#### Main Receipt Printing (`PrintMainReceiptAsync`)
The system now uses intelligent printing that checks:

1. **Printer Active Status**: Only prints to active printers
2. **Main Receipt Enabled**: Only prints to printers with `MainReceipt = true`
3. **Copy Count**: Prints the specified number of copies (`MainReceiptCount`)

```csharp
// Example validation flow:
foreach (var printer in printersService.Printers)
{
    // Step 1: Check if printer is active
    if (!printer.IsActive) continue;
    
    // Step 2: Check if main receipt is enabled
    var settings = PrinterSettingsService.Instance.GetPrinterSettings(printer.DeviceName);
    if (settings == null || !settings.MainReceipt) continue;
    
    // Step 3: Print specified number of copies
    for (int i = 0; i < settings.MainReceiptCount; i++)
    {
        await PrintToPrinterAsync(printer.DeviceName, receiptContent);
    }
}
```

#### Kitchen Receipt Printing (`PrintKitchenReceiptAsync`)
Similar logic for kitchen receipts:
1. **Printer Active Status**: Only prints to active printers
2. **Kitchen Receipt Enabled**: Only prints to printers with `KitchenReceipt = true`
3. **Copy Count**: Prints the specified number of copies (`KitchenReceiptCount`)

### 4. Receipt Content

#### Main Receipt Content
- Full customer receipt with prices, totals, and payment information
- Includes shop details, customer info, order items with prices
- Payment method and transaction details

#### Kitchen Receipt Content
- Kitchen-focused receipt without prices
- Order details, customer info, table number (for dine-in)
- Item quantities and modifiers
- Special instructions and order notes

## Usage

### Accessing Printer Settings
1. Go to **Settings** page
2. Click the **cog icon** next to any printer
3. Configure the settings in the dialog:
   - **Status**: Active/Inactive
   - **Main Receipt**: Enable/disable with copy count
   - **Kitchen Receipt**: Enable/disable with copy count

### Automatic Printing
- **Main Receipts**: Automatically printed when orders are placed
- **Kitchen Receipts**: Can be called separately when needed
- **Validation**: System automatically validates printer settings before printing

## File Structure
```
printers.txt (on Desktop)
[
  {
    "DeviceName": "POS-58 Printer",
    "ConnectedVia": "USB",
    "IsActive": true,
    "MainReceipt": true,
    "KitchenReceipt": false,
    "MainReceiptCount": 1,
    "KitchenReceiptCount": 1,
    "PaperSize": "80mm",
    "PrintWidth": 32,
    "AutoCut": true,
    "FontSize": "Normal",
    "BoldHeader": true,
    "ShowLogo": false,
    "CustomSettings": ""
  }
]
```

## Benefits
- **Flexible Configuration**: Each printer can be configured independently
- **Local Persistence**: Settings survive application restarts
- **Intelligent Printing**: Only prints to appropriate printers
- **Copy Control**: Specify exact number of copies for each receipt type
- **Easy Management**: Simple UI for managing printer settings
