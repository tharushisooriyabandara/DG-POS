# Firebase Connection Fix - Temp File Management

## 🐛 Problem Statement
Firebase was not connecting properly or using **old/stale credentials** because the app was creating temporary credential files with random names in the Windows temp folder that were not being cleaned up properly.

### Symptoms:
- Firebase fails to connect after app has been idle
- Connection works sometimes but not others
- Old credentials are cached somewhere
- Multiple temp files accumulate in Windows temp folder

## 🔍 Root Cause Analysis

### The Original Code:
```csharp
// OLD CODE - PROBLEMATIC
var tempPath = Path.Combine(Path.GetTempPath(), $"firebase-{Guid.NewGuid():N}.json");
File.WriteAllBytes(tempPath, jsonBytes);
try
{
    credential = GoogleCredential.FromFile(tempPath);
}
finally
{
    try { File.Delete(tempPath); } catch { }
}
```

### Issues Identified:
1. **Random File Names**: Each initialization created a new file with a random GUID
   - `firebase-a1b2c3d4e5f6.json`
   - `firebase-f6e5d4c3b2a1.json`
   - Files accumulated over time

2. **Failed Deletions**: The `finally` block tried to delete the temp file, but:
   - File might be locked by Firebase SDK
   - File permissions might prevent deletion
   - Silent failures (`catch { }`) meant files remained

3. **Stale Credentials**: Old temp files could be picked up by subsequent app instances

4. **No Cleanup**: No mechanism to clean up files from previous runs

## ✅ Applied Fixes

### 1. Consistent Temp File Path
**Change:** Use a fixed file path instead of random GUIDs

**Before:**
```csharp
var tempPath = Path.Combine(Path.GetTempPath(), $"firebase-{Guid.NewGuid():N}.json");
```

**After:**
```csharp
var appTempFolder = Path.Combine(Path.GetTempPath(), "DeliverGate_POS");
Directory.CreateDirectory(appTempFolder);
var tempPath = Path.Combine(appTempFolder, "firebase-credentials.json");
```

**Benefits:**
- ✅ Always know where the temp file is
- ✅ Easy to track and clean up
- ✅ One file instead of many

---

### 2. Always Use Fresh Credentials from POS Folder
**Change:** Delete old temp file before creating new one

**Added:**
```csharp
// Delete old temp file if exists to ensure we use fresh credentials
if (File.Exists(tempPath))
{
    try 
    { 
        File.Delete(tempPath);
        System.Diagnostics.Debug.WriteLine("[Firebase] Deleted old temp credentials file");
    } 
    catch (Exception delEx) 
    { 
        System.Diagnostics.Debug.WriteLine($"[Firebase] Warning: Could not delete old temp file: {delEx.Message}");
    }
}

// Write fresh credentials from the POS folder
File.WriteAllBytes(tempPath, jsonBytes);
```

**Benefits:**
- ✅ Always reads from `firebase-adminsdk.enc` in POS folder
- ✅ No cached credentials
- ✅ Updates take effect immediately

---

### 3. Automatic Cleanup on Startup
**New Method:** `CleanupOldTempFiles()`

Called at the beginning of `InitializeFirebaseAsync()`:

```csharp
private void CleanupOldTempFiles()
{
    try
    {
        // Clean up our app's temp folder
        var appTempFolder = Path.Combine(Path.GetTempPath(), "DeliverGate_POS");
        if (Directory.Exists(appTempFolder))
        {
            var files = Directory.GetFiles(appTempFolder, "firebase-*.json");
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Cleaned up old temp file: {file}");
                }
                catch { }
            }
        }
        
        // Also clean up any random-named files from old code
        var tempPath = Path.GetTempPath();
        if (Directory.Exists(tempPath))
        {
            var oldFiles = Directory.GetFiles(tempPath, "firebase-*.json")
                .Where(f => f.Contains("firebase-") && f.EndsWith(".json"))
                .ToArray();
            
            foreach (var file in oldFiles)
            {
                try
                {
                    // Only delete files older than 1 hour to avoid conflicts
                    var fileInfo = new FileInfo(file);
                    if ((DateTime.Now - fileInfo.LastWriteTime).TotalHours > 1)
                    {
                        File.Delete(file);
                    }
                }
                catch { }
            }
        }
    }
    catch { }
}
```

**Benefits:**
- ✅ Removes leftover files from previous runs
- ✅ Cleans up files from old code (random GUIDs)
- ✅ Prevents accumulation of temp files
- ✅ Safe - only deletes old files (>1 hour)

---

### 4. Enhanced Logging
**Added debug messages** to track what's happening:

```csharp
System.Diagnostics.Debug.WriteLine($"[Firebase] Loading encrypted credentials from: {encPath}");
System.Diagnostics.Debug.WriteLine($"[Firebase] Writing decrypted credentials to temp: {tempPath}");
System.Diagnostics.Debug.WriteLine("[Firebase] Successfully loaded credentials from temp file");
System.Diagnostics.Debug.WriteLine("[Firebase] Deleted temp credentials file after loading");
```

**Benefits:**
- ✅ Easy to debug connection issues
- ✅ See exactly which file is being used
- ✅ Track cleanup operations

---

## 📊 Impact

### Before Fix:
```
Windows Temp Folder:
├── firebase-a1b2c3d4.json (old, stale)
├── firebase-e5f6d4c3.json (old, stale)
├── firebase-9f8e7d6c.json (current?)
└── ... many more ...

Result: ❌ Unpredictable behavior, stale credentials
```

### After Fix:
```
C:\Users\[User]\AppData\Local\Temp\DeliverGate_POS\
└── firebase-credentials.json (fresh, from POS folder)

Result: ✅ Always uses current credentials from firebase-adminsdk.enc
```

---

## 🔧 Technical Details

### File Locations:

**Source (Encrypted):**
```
C:\Users\[User]\Documents\Projects\pos-ui-wpf\firebase-adminsdk.enc
```

**Temporary (Decrypted):**
```
C:\Users\[User]\AppData\Local\Temp\DeliverGate_POS\firebase-credentials.json
```

### Process Flow:

1. **On App Startup:**
   - Clean up all old temp files (>1 hour old)
   - Remove files from `DeliverGate_POS` folder

2. **On Firebase Initialization:**
   - Read `firebase-adminsdk.enc` from POS folder
   - Decrypt using encryption key
   - Delete any existing temp file
   - Write fresh decrypted JSON to temp
   - Load credentials from temp file
   - Attempt to delete temp file immediately

3. **Safety Mechanisms:**
   - All deletions are in try-catch (won't crash if fails)
   - Only deletes files older than 1 hour (prevents conflicts)
   - Dedicated temp folder for easy identification

---

## 📝 Testing Checklist

After applying this fix:

- [ ] Firebase connects successfully on app startup
- [ ] Firebase reconnects after app idle time
- [ ] Updating `firebase-adminsdk.enc` takes effect on restart
- [ ] Check Debug output for `[Firebase]` messages
- [ ] Verify temp folder: `%TEMP%\DeliverGate_POS\`
- [ ] Confirm no accumulation of old temp files

---

## 🔍 Debugging

If Firebase still has connection issues:

1. **Check Debug Output Window:**
   Look for messages starting with `[Firebase]`

2. **Verify Source File:**
   ```
   C:\Users\[User]\Documents\Projects\pos-ui-wpf\firebase-adminsdk.enc
   ```
   - File exists?
   - Not corrupted?
   - Correct encryption key available?

3. **Check Temp Folder:**
   ```
   C:\Users\[User]\AppData\Local\Temp\DeliverGate_POS\
   ```
   - Folder exists?
   - Can write files there?
   - Files getting deleted properly?

4. **Enable Detailed Logging:**
   All operations are logged with `[Firebase]` prefix

---

## 🎯 Files Modified

- ✅ **`Services\FirebaseService.cs`** - Complete temp file management overhaul

## 🗑️ Files Deleted

- ✅ **`Services\FirebaseConfig.cs`** - Removed obsolete hardcoded configuration
- ✅ **`Models\FirebaseConfigModel.cs`** - Removed duplicate hardcoded configuration

**Reason:** Firebase configuration is now extracted directly from the service account JSON file (single source of truth).

### Changes Made:
1. Added `CleanupOldTempFiles()` method
2. Changed temp file path to use fixed name
3. Added pre-deletion of old temp files
4. Enhanced logging throughout
5. Better error handling and diagnostics

---

## 📞 Support

If issues persist:

1. Check Debug output for `[Firebase]` messages
2. Verify `firebase-adminsdk.enc` is in POS folder
3. Ensure encryption key is accessible
4. Check Windows temp folder permissions
5. Look for file lock issues (antivirus, etc.)

**Last Updated:** February 9, 2026
**Fixed By:** AI Assistant
