# Firebase Setup Guide for WPF Application

## Overview
Your WPF application is now integrated with Firebase Firestore, but it requires proper authentication credentials to work. The `apiKey` from your Firebase config is not sufficient for server-side/desktop applications.

## Step 1: Get Service Account Key

1. **Go to Firebase Console**
   - Visit [https://console.firebase.google.com/](https://console.firebase.google.com/)
   - Select your project: `testdelivergate`

2. **Navigate to Project Settings**
   - Click the gear icon (⚙️) next to "Project Overview"
   - Select "Project settings"

3. **Go to Service Accounts Tab**
   - Click on the "Service accounts" tab
   - You'll see "Firebase Admin SDK" section

4. **Generate New Private Key**
   - Click "Generate new private key" button
   - Click "Generate key" in the popup
   - This will download a JSON file (e.g., `testdelivergate-firebase-adminsdk-xxxxx-xxxxxxxxxx.json`)

## Step 2: Set Environment Variable

### Option A: Set Environment Variable (Recommended)
1. **Save the JSON file** in a secure location (e.g., `C:\Firebase\testdelivergate-key.json`)
2. **Set Environment Variable**:
   - Open Command Prompt as Administrator
   - Run: `setx GOOGLE_APPLICATION_CREDENTIALS "C:\Firebase\testdelivergate-key.json"`
   - Restart your application

### Option B: Set Environment Variable for Current Session
1. **Open Command Prompt**
2. **Run**: `set GOOGLE_APPLICATION_CREDENTIALS=C:\Firebase\testdelivergate-key.json`
3. **Start your application from the same command prompt**

### Option C: Set in Windows System Properties
1. **Right-click on "This PC"** → Properties
2. **Click "Advanced system settings"**
3. **Click "Environment Variables"**
4. **Under "System variables"**, click "New"
5. **Variable name**: `GOOGLE_APPLICATION_CREDENTIALS`
6. **Variable value**: Full path to your JSON file
7. **Click OK** and restart your application

## Step 3: Test the Integration

1. **Run your WPF application**
2. **Navigate to the Cashier page**
3. **You should see an alert** indicating whether the `subway_wb8906` collection exists

## Troubleshooting

### Error: "Firebase credentials not found"
- **Solution**: Make sure the `GOOGLE_APPLICATION_CREDENTIALS` environment variable is set correctly
- **Check**: Verify the JSON file path exists and is accessible

### Error: "Permission denied" or "Unauthorized"
- **Solution**: Ensure the service account has proper permissions in Firebase
- **Check**: Go to Firebase Console → Project Settings → Service Accounts → Manage service account permissions

### Error: "Project not found"
- **Solution**: Verify your project ID is correct in `FirebaseConfig.cs`
- **Current project ID**: `testdelivergate`

## Security Notes

⚠️ **Important Security Considerations**:
- Keep your service account key file secure and never commit it to version control
- The JSON file contains sensitive credentials
- Consider using Azure Key Vault or similar services for production applications
- Rotate your service account keys regularly

## Current Implementation

Your application now:
- ✅ Automatically checks Firebase collection when Cashier page loads
- ✅ Shows alert indicating if `subway_wb8906` collection exists
- ✅ Uses lazy initialization to prevent UI blocking
- ✅ Provides clear error messages for authentication issues

## Files Modified

1. **Services/FirebaseService.cs** - Main Firebase integration service
2. **Services/FirebaseConfig.cs** - Firebase configuration
3. **ViewModels/CashierHomeViewModel.cs** - Automatic collection checking
4. **POS_UI.csproj** - Added Firebase NuGet packages

## Next Steps

Once you have the service account key set up:
1. The application will automatically check the Firebase collection on Cashier page load
2. You'll see an alert showing whether the collection exists
3. You can extend the functionality to read/write data from Firebase as needed

## Support

If you encounter any issues:
1. Check the error messages in the application
2. Verify your environment variable is set correctly
3. Ensure your Firebase project has the correct permissions
4. Check that the `subway_wb8906` collection exists in your Firebase Firestore database 