# Firebase Troubleshooting Guide - Complete Checklist

## 🔍 All Potential Issues That Can Cause Firebase to Fail

### ✅ **Already Fixed:**
1. ✅ **Temp file caching issue** - Now using consistent temp file path
2. ✅ **Stale credentials** - Always reads fresh from POS folder

---

### 🔴 **Other Common Issues to Check:**

## 1. **Settings File Issues** ⚠️

### **Problem:** Collection Name Not Found
Firebase collection name is built from `settings.txt`:
```
Collection Name = {tenantCode}_{outletCode}
Example: subway_wb8906
```

**Check:**
```
Location: C:\Users\[User]\Documents\Projects\pos-ui-wpf\settings.txt

Expected format:
subway     # Line 1: tenant_code
wb8906     # Line 2: outlet_code
123        # Line 3: brand_id
```

**Symptoms:**
- Firebase initializes but listener doesn't receive events
- Wrong collection is being monitored
- Default collection "subway_wb8906" is used instead of your settings

**Fix:**
- Ensure `settings.txt` exists in the same folder as the executable
- Verify tenant code and outlet code match your Firebase collection name
- Check there are no extra spaces or line breaks

---

## 2. **Firestore Database Rules** 🔒

### **Problem:** Permission Denied
Your Firebase credentials might be valid, but Firestore rules block access.

**Check Firebase Console:**
1. Go to: https://console.firebase.google.com/
2. Select project: `testdelivergate`
3. Navigate to: **Firestore Database → Rules**

**Common Rule Issues:**
```javascript
// BAD - Blocks all access
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /{document=**} {
      allow read, write: if false;  // ❌ Blocks everything!
    }
  }
}

// GOOD - Allows service account access
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /{document=**} {
      allow read, write: if request.auth != null;  // ✅ Allows authenticated users
    }
  }
}
```

**Symptoms:**
- Error: "PERMISSION_DENIED"
- Error: "Missing or insufficient permissions"
- Connection works but can't read/write data

**Fix:**
- Update Firestore rules to allow service account access
- For development, you can temporarily use: `allow read, write: if true;` (NOT for production!)

---

## 3. **Network/Firewall Issues** 🌐

### **Problem:** Can't Reach Firebase Servers

**Check:**
- **Firewall:** Windows Firewall or antivirus blocking connections
- **Proxy:** Corporate proxy blocking Google APIs
- **DNS:** Can't resolve `firestore.googleapis.com`
- **Ports:** Firewall blocking HTTPS (port 443)

**Test Network Connectivity:**
```powershell
# Test if you can reach Firebase
Test-NetConnection firestore.googleapis.com -Port 443

# Expected result: TcpTestSucceeded : True
```

**Symptoms:**
- Timeout errors
- "Cannot connect to remote server"
- Works at home but not at office (corporate firewall)
- Long delays before error

**Fix:**
- Add exception to Windows Firewall
- Configure corporate proxy settings
- Contact IT department for firewall rules

---

## 4. **Project ID Mismatch** 🆔

### **Problem:** Wrong Firebase Project

Firebase service uses **two sources** for project ID:
1. From `firebase-adminsdk.enc` (decrypted JSON has `project_id`)
2. From `FirebaseConfig.cs` hardcoded as `testdelivergate`

**Check:**
```csharp
// File: Services\FirebaseConfig.cs
public static readonly Dictionary<string, string> Config = new Dictionary<string, string>
{
    { "projectId", "testdelivergate" },  // ← Must match your Firebase project!
    ...
};
```

**And in your credentials file:**
```json
{
  "project_id": "testdelivergate",  // ← Must match!
  ...
}
```

**Symptoms:**
- Error: "Project not found"
- Error: "The caller does not have permission"
- Collections exist but can't be accessed

**Fix:**
- Verify project ID in both locations matches
- Check Firebase Console for correct project name
- Ensure credentials file is for the correct project

---

## 5. **Encryption Key Issues** 🔐

### **Problem:** Can't Decrypt firebase-adminsdk.enc

**Check:**
```
File: encryption.key
Location: Same folder as firebase-adminsdk.enc
```

**Symptoms:**
- Error: "Padding is invalid and cannot be removed"
- Error: "The input is not a valid Base-64 string"
- Firebase initialization fails silently
- No error message (caught and swallowed)

**Fix:**
- Ensure `encryption.key` file exists
- Verify it's the correct key used to encrypt the file
- Check file hasn't been corrupted
- Re-encrypt firebase credentials if key is lost

---

## 6. **Collection Doesn't Exist** 📁

### **Problem:** Monitoring Non-Existent Collection

**Check:**
The code builds collection name as: `{tenantCode}_{outletCode}`

Example: If settings.txt has:
```
subway
wb8906
```
→ Collection name: `subway_wb8906`

**Verify in Firebase Console:**
1. Open Firestore Database
2. Look for collection named `subway_wb8906`
3. Check it has documents

**Symptoms:**
- Listener starts but never receives events
- No errors but nothing happens
- Debug shows: "Listener started successfully"
- But no "Received snapshot" messages

**Fix:**
- Create the collection in Firebase if missing
- Add at least one document to the collection
- Verify collection name matches settings

---

## 7. **Multiple App Instances** 🔄

### **Problem:** Multiple Instances Competing for Resources

**Check:**
- Task Manager → Check how many instances of your POS app are running
- Only ONE instance should have the Firebase listener active

**Symptoms:**
- Inconsistent behavior
- Sometimes works, sometimes doesn't
- Temp file conflicts
- Lock file issues

**Fix:**
- Close all instances of the app
- Restart just ONE instance
- Implement single-instance detection in code

---

## 8. **Listener Never Starts** 🎧

### **Problem:** Firebase initialized but listener not activated

**Check where listener starts:**
```csharp
// File: Services\GlobalDataService.cs
await _firebaseService.StartListeningToCollectionAsync();
```

**When does it start?**
- When user logs in successfully
- After `InitializeGlobalDataAsync()` is called

**Symptoms:**
- Firebase shows as initialized
- No "[Firebase] Starting listener" debug message
- No collection change events

**Fix:**
- Check Debug output for "[Firebase]" messages
- Verify login process completes successfully
- Ensure `InitializeGlobalDataAsync()` is called

---

## 9. **Reconnect Attempts Exhausted** 🔁

### **Problem:** Listener gave up after max retries

**Check:**
```csharp
private const int MaxReconnectAttempts = 10;
```

**Symptoms:**
- Debug shows: "Max reconnect attempts reached"
- Listener was working but stopped
- No more automatic reconnection

**Fix:**
- Restart the application
- Check what caused the repeated failures (network, firewall, rules)
- Increase `MaxReconnectAttempts` if needed

---

## 10. **Service Account Permissions** 👤

### **Problem:** Service Account Lacks Permissions

**Check in Firebase Console:**
1. Go to: IAM & Admin
2. Find your service account email
3. Verify it has role: **Cloud Datastore User** or **Owner**

**Symptoms:**
- Error: "Permission denied"
- Error: "User not authorized"
- Can connect but can't read/write

**Fix:**
- Grant proper IAM roles to service account
- Re-download service account key with correct permissions

---

## 📝 **Diagnostic Checklist**

Run through this checklist:

### **Files:**
- [ ] `firebase-adminsdk.enc` exists in POS folder
- [ ] `encryption.key` exists in same folder
- [ ] `settings.txt` exists with valid tenant/outlet codes
- [ ] All files are not corrupted

### **Firebase Console:**
- [ ] Project ID matches (`testdelivergate`)
- [ ] Collection exists: `{tenantCode}_{outletCode}`
- [ ] Collection has at least one document
- [ ] Firestore rules allow service account access
- [ ] Service account has correct IAM permissions

### **Network:**
- [ ] Can ping `firestore.googleapis.com`
- [ ] Port 443 (HTTPS) not blocked
- [ ] No proxy issues
- [ ] Windows Firewall allows app

### **Application:**
- [ ] Only ONE instance running
- [ ] User logged in successfully
- [ ] `InitializeGlobalDataAsync()` called
- [ ] Check Debug output for `[Firebase]` messages

### **Temp Files:**
- [ ] Check: `%TEMP%\DeliverGate_POS\`
- [ ] Should have 0 or 1 file only
- [ ] No old files accumulating

---

## 🔍 **How to Debug**

### **1. Enable All Debug Messages**
Check Debug Output window for messages starting with `[Firebase]`

### **2. Test Connection Manually**
Add a test button to your UI:
```csharp
await _firebaseService.TestFirebaseConnectionAsync();
```

### **3. Check Collection Name**
Add this to see what collection is being monitored:
```csharp
System.Diagnostics.Debug.WriteLine($"[Firebase] Collection name: {GetCollectionNameFromSettings()}");
```

### **4. Verify Initialization**
```csharp
System.Diagnostics.Debug.WriteLine($"[Firebase] Is initialized: {_firestoreDb != null}");
System.Diagnostics.Debug.WriteLine($"[Firebase] Is listening: {_isListening}");
```

---

## 🚀 **Quick Fix Priority Order**

Try these in order:

1. **Restart app** - Clears any stuck state
2. **Check settings.txt** - Most common issue
3. **Verify collection exists in Firebase** - Second most common
4. **Check Firestore rules** - Often overlooked
5. **Test network connectivity** - Firewall/proxy issues
6. **Verify project ID** - Credentials for wrong project
7. **Check temp files** - Already fixed, but verify
8. **Review Debug output** - See what's actually happening

---

## 📞 **Still Not Working?**

Check Debug Output for these specific messages:

```
✅ Good messages:
[Firebase] Temp file cleanup completed
[Firebase] Loading encrypted credentials from: ...
[Firebase] Successfully loaded credentials from temp file
[Firebase] Firebase initialized successfully with project: testdelivergate
[Firebase] Starting listener for collection: subway_wb8906
[Firebase] Listener started successfully
[Firebase] Received snapshot with X changes

❌ Bad messages:
[Firebase] ERROR: Encrypted credentials not found
[Firebase] Firebase initialization error
[Firebase] Cannot start listener - Firestore DB not initialized
[Firebase] Max reconnect attempts reached
[Firebase] Listener error: PERMISSION_DENIED
```

---

**Last Updated:** February 9, 2026
**Created By:** AI Assistant
