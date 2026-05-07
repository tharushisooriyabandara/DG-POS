# Menu Configuration - Save & Load System

## Overview
The system **already saves and loads** selected categories and items to/from the API! Here's how it works:

## How Saving Works

### 1. When You Create/Edit a Tab
```
User selects categories OR items → Click Save → Tab saved with IDs
```

### 2. Data Stored in Tab
Each `MenuTabModel` contains:
- `ContentType`: "categories" or "items"
- `CategoryIds`: List of selected category IDs (empty = all categories)
- `ItemIds`: List of selected item IDs (empty = all items)

**Example:**
```json
{
  "id": 2,
  "name": "Drinks",
  "order": 2,
  "contentType": "categories",
  "categoryIds": [5, 8, 12],  // Selected category IDs
  "itemIds": []                 // Empty because it's a category tab
}
```

### 3. Saving to API
When you click **"SAVE"** button in Settings:
1. Collects all tabs from `MenuTabs` list
2. Serializes to JSON with camelCase
3. Calls API: `PATCH /api/v1/shop/{shopId}/config/menu`
4. Sends the complete configuration

**Location:** `ViewModels/SettingsViewModel.cs` → `SaveMenuConfigAsync()`

## How Loading Works

### 1. When App Starts
```
Load Settings Page → LoadMenuConfigAsync() → Parse JSON → Restore tabs with IDs
```

### 2. Loading from API
1. Calls `GET /api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal=1`
2. Parses JSON response
3. Extracts `categoryIds` and `itemIds` arrays
4. Restores selected state in the edit dialog

**Location:** `Services/MenuConfigService.cs` → `LoadMenuConfigAsync()`

### 3. Restoring Selections
When you edit an existing tab:
```csharp
// In EditMenuTabViewModel.cs constructor:
IsSelected = tab.CategoryIds.Contains(categoryId)  // Mark as selected
IsSelected = tab.ItemIds.Contains(itemId)          // Mark as selected
```

## Debug Output

### When Adding Tab:
```
[EditMenuTab] Saving as CATEGORIES menu: 3 selected (empty = all)
[SettingsVM] ✓ Added CATEGORIES tab: 'Beverages' with 3 selected categories (IDs: 5,8,12)
```

### When Updating Tab:
```
[EditMenuTab] Saving as ITEMS menu: 5 selected (empty = all)
[SettingsVM] ✓ Updated ITEMS tab: 'Popular' with 5 selected items (IDs: 101,102,105,108,112)
```

### When Loading:
```
[MenuConfig] Loading from API: shopId=1, brandId=1, terminalId=1
[MenuConfig] ✓ Parsed tab: 'Beverages' (ID=2, Order=2, Type=categories, Default=false)
[MenuConfig] ✓ Loaded successfully: 3 tabs
```

## File Locations

### Models
- `Models/MenuTabModel.cs` - Contains `CategoryIds` and `ItemIds` properties

### Services
- `Services/MenuConfigService.cs` - Handles save/load to API
  - `LoadMenuConfigAsync()` - Lines 56-257
  - `SaveMenuConfigAsync()` - Lines 262-320

### ViewModels
- `ViewModels/SettingsViewModel.cs` - Manages menu configuration
  - `AddMenuTabAsync()` - Lines 2848-2900
  - `EditMenuTabAsync()` - Lines 2905-2969
  - `SaveMenuConfigAsync()` - Lines 3074-3122

- `ViewModels/EditMenuTabViewModel.cs` - Handles tab editing
  - Constructor loads existing selections - Lines 84-152
  - `Save()` method saves IDs - Lines 220-254

## API Endpoints

### Get Configuration
```
GET /api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal=1
```

### Save Configuration
```
PATCH /api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal=1

Body:
{
  "brandId": 1,
  "outletId": 1,
  "terminalId": "1",
  "tabs": [
    {
      "id": 1,
      "name": "All Items",
      "order": 1,
      "isDefault": true,
      "contentType": "items",
      "categoryIds": [],
      "itemIds": []
    },
    {
      "id": 2,
      "name": "Drinks",
      "order": 2,
      "isDefault": false,
      "contentType": "categories",
      "categoryIds": [5, 8, 12],
      "itemIds": []
    }
  ]
}
```

## Testing

### To Test Saving:
1. Open Settings → Menu section
2. Click "Add Tab"
3. Enter tab name (e.g., "Beverages")
4. Click "Categories" button
5. Select some categories
6. Click "SAVE"
7. Watch debug output for: `✓ Added CATEGORIES tab: 'Beverages' with X selected categories (IDs: ...)`
8. Click "SAVE" button (green) in Settings to save to API

### To Test Loading:
1. Close app completely
2. Restart app
3. Open Settings → Menu section
4. Watch debug output for: `[MenuConfig] ✓ Loaded successfully: X tabs`
5. Click "Edit" on your saved tab
6. The selected categories/items should have checkmarks ✓

## Summary

✅ **System is complete!** 
- Selected categories/items are saved as ID arrays
- IDs are persisted to API in JSON format
- IDs are loaded back when app starts
- Selections are restored when editing tabs

**No additional code needed** - just test it!
