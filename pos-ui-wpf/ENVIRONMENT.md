### Environment configuration (Development/Production)

This app now reads URLs and file names from JSON config, per environment.

### Files
- `appsettings.json`: base defaults
- `appsettings.Development.json`: overrides for Development
- `appsettings.Production.json`: overrides for Production

All three are copied to the output/publish folder. The app loads `appsettings.json` and then `appsettings.{ENV}.json` based on the environment name.

### Environment selection
The environment is detected in this order:
1. `POS_ENV`
2. `DOTNET_ENVIRONMENT`
3. `ASPNETCORE_ENVIRONMENT`

If none are set, it defaults to `Development`.

### How to run (PowerShell on Windows)
- Development (default):
```powershell
# Nothing required; or explicitly
$env:POS_ENV = "Development"
dotnet run --project .
```

- Production:
```powershell
$env:POS_ENV = "Production"
dotnet run --project .
```

### How to publish
Environment is chosen at runtime of the published EXE. Set the env var before launching:

```powershell
# Publish
dotnet publish -c Release

# Run Development build
$env:POS_ENV = "Development"
& .\bin\Release\net8.0-windows\publish\POS_UI.exe

# Run Production build
$env:POS_ENV = "Production"
& .\bin\Release\net8.0-windows\publish\POS_UI.exe
```

To set permanently (system/user):
```powershell
setx POS_ENV Production
```
Re-open shells/apps to pick up changes.

### Config schema
```json
{
  "EnvironmentName": "Development | Production",
  "Urls": {
    "GoApiBaseUrl": "...",
    "UserApiBaseUrl": "...",
    "ReportingBaseUrl": "...",
    "PlatformBaseUrl": "...",
    "AdminBaseUrl": "..."
  },
  "Files": {
    "SettingsFileName": "settings.txt",
    "GoogleMapsApiKeyFileName": "google-maps-api-key.txt",
    "FirebaseServiceAccountFileName": "...json",
    "GoogleMapsApiKeyEncryptedFileName": "google-maps-api-key.enc",
    "FirebaseServiceAccountEncryptedFileName": "firebase-adminsdk.enc"
  },
  "Auth": {
    "LaravelClientId": "...",
    "LaravelClientSecret": "..."
  },
  "Security": {
    "UsbKeyFileName": "keyfile.txt",
    "RequireUsbKeyOnEveryRun": false,
    "EncryptionKeyCredentialName": "POS_UI/EncryptionKey"
  }
}
```

### Notes
- Update the URLs in `appsettings.Production.json` to your live hosts.
- File names under `Files` can be customized per environment (e.g., a different service-account JSON).
- Secrets: For Production, prefer keeping `Auth` values empty and set via environment variables or another secure channel.

### Encryption flow (Production)
- On first run, insert a USB drive containing `keyfile.txt` at the root. The file must contain a Base64-encoded 32-byte key.
- The app reads the key and saves it to Windows Credential Manager under `POS_UI/EncryptionKey` (unless `RequireUsbKeyOnEveryRun` is true).
- The app decrypts `google-maps-api-key.enc` and `firebase-adminsdk.enc` at runtime.
- If `RequireUsbKeyOnEveryRun` is true, the USB key is required each time and the key is not taken from Credential Manager.


