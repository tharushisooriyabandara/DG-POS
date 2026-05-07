# Testing Menu Configuration Save & Load

## Quick Test Steps

### 1. Test Saving
1. Open app and go to Settings → Menu tab
2. Click "Add Tab" button
3. Name it "Test Drinks"
4. Click "Categories" button
5. Select 2-3 categories (click on them - they'll show checkmarks)
6. Click "SAVE" button (in the dialog)
7. **Click "Save Menu Configuration" button** (blue button that appears)

**Expected Debug Output:**
```
======================================
[SettingsVM] ========== SAVING MENU CONFIGURATION ==========
[SettingsVM] Total tabs to save: 2
[SettingsVM] Tab #1: 'All Items' (Type=items, Default=True)
           → ItemIds: []
[SettingsVM] Tab #2: 'Test Drinks' (Type=categories, Default=False)
           → CategoryIds: [5, 8, 12]
[SettingsVM] Calling MenuConfigService.SaveMenuConfigAsync...
[MenuConfig] Saving to API: 2 tabs
[MenuConfig] JSON: { ... }
[ApiService] PATCH /api/v1/shop/1/config/menu?brand=1&terminal=1
[ApiService] Request Body (before wrapping): { ... }
[ApiService] Request Body (after wrapping): { "data": { ... } }
[ApiService] Response Status: 200
[ApiService] ✓ Menu config saved successfully to API
[SettingsVM] ✓✓✓ Menu configuration SAVED SUCCESSFULLY ✓✓✓
======================================
```

### 2. Test Loading
1. Close the app completely
2. Reopen the app
3. Login
4. Go to Settings → Menu tab

**Expected Debug Output:**
```
======================================
[SettingsVM] ========== LOADING MENU CONFIGURATION ==========
[MenuConfig] Loading from API: shopId=1, brandId=1, terminalId=1
[ApiService] GET /api/v1/shop/1/config/menu?brand=1&terminal=1
[ApiService] Response Status: 200
[ApiService] Menu config loaded successfully, length: 542
[MenuConfig] ✓ Parsed tab: 'All Items' (ID=1, Order=1, Type=items, Default=true)
[MenuConfig] ✓ Parsed tab: 'Test Drinks' (ID=2, Order=2, Type=categories, Default=false)
[MenuConfig] ✓ Loaded successfully: 2 tabs
[SettingsVM] Received 2 tabs from API
[SettingsVM] Tab #1: 'All Items' (Type=items, Default=True)
           → ItemIds: []
[SettingsVM] Tab #2: 'Test Drinks' (Type=categories, Default=False)
           → CategoryIds: [5, 8, 12]
[SettingsVM] ✓✓✓ Loaded 2 menu tabs successfully ✓✓✓
======================================
```

### 3. Test Edit - Verify Selections Restored
1. Click "Edit" on the "Test Drinks" tab
2. Check if the categories you selected have checkmarks ✓

**Expected:**
- The 3 categories you selected should show checkmarks
- Other categories should not have checkmarks

**Expected Debug Output:**
```
[EditMenuTab] Switched to Categories view
(Dialog opens with checkmarks on previously selected categories)
```

## If It's Not Working

### Check Debug Output for Errors

1. **If Save fails:**
```
[ApiService] ✗ Error saving menu config: [error message]
```
- Check if API endpoint is correct
- Check if you're logged in
- Check network connection

2. **If Load fails:**
```
[MenuConfig] Error loading from API: [error message]
```
- Check if API is accessible
- Check if shop/brand IDs are valid

3. **If API returns 404:**
```
[ApiService] Menu config not found (404), returning empty
[MenuConfig] No config found, creating default
```
- This is **NORMAL** for first time - default config will be created

### Common Issues

**Issue:** "Save Menu Configuration" button doesn't appear
**Solution:** Make changes to a tab first (add/edit/delete) to trigger `HasUnsavedChanges = true`

**Issue:** Categories/Items not selected when editing
**Solution:** Check debug output for the CategoryIds/ItemIds arrays - they should match what was saved

**Issue:** API returns 401 Unauthorized
**Solution:** Re-login to the app

## Summary

✅ Save is working when you see: `✓ Menu config saved successfully to API`
✅ Load is working when you see: `✓ Loaded successfully: X tabs`
✅ Restore is working when editing shows checkmarks on previously selected items
