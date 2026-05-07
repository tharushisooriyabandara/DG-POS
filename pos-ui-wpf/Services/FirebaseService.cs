using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Google.Api.Gax.Grpc;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Generic;
using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Api.Gax;
using System.Linq;
using System.Threading;

namespace POS_UI.Services
{
    public class FirebaseService
    {
        private FirestoreDb _firestoreDb;
        private bool _isInitialized = false;
        private readonly object _lockObject = new object();
        
        // Real-time listener properties
        private FirestoreChangeListener _firestoreListener;
        private bool _isListening = false;
        private CancellationTokenSource _listenerCancellationTokenSource;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 10;
        private string _activeCollectionName;
        private DateTime _lastSuccessfulConnection = DateTime.MinValue;
        private Timer _healthCheckTimer;
        
        // Event for collection changes
        public event Action<string, string> OnCollectionChanged;

        public FirebaseService()
        {
            // Don't initialize in constructor to avoid blocking UI
        }
        
        /// <summary>
        /// Cleans up old temporary Firebase credential files to prevent using cached/stale credentials
        /// </summary>
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
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Could not delete temp file {file}: {ex.Message}");
                        }
                    }
                }
                
                // Also clean up any random-named files from old code (firebase-{guid}.json pattern)
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
                                System.Diagnostics.Debug.WriteLine($"[Firebase] Cleaned up old random temp file: {file}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Could not delete old temp file {file}: {ex.Message}");
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[Firebase] Temp file cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] Error during temp file cleanup: {ex.Message}");
                // Don't fail initialization if cleanup fails
            }
        }

        private string GetCollectionNameFromSettings()
        {
            try
            {
                var settingsService = new SettingsService();
                var (tenantCode, outletCode, _) = settingsService.LoadSettings();
                
                if (!string.IsNullOrWhiteSpace(tenantCode) && !string.IsNullOrWhiteSpace(outletCode))
                {
                    return $"{tenantCode}_{outletCode}";
                }
                
                // Fallback to default collection name if settings are not available
                return "subway_wb8906";
            }
            catch (Exception ex)
            {
                // Fallback to default collection name if there's an error
                return "subway_wb8906";
            }
        }

        private async Task InitializeFirebaseAsync()
        {
            if (_isInitialized) return;

            lock (_lockObject)
            {
                if (_isInitialized) return;
            }

            try
            {
                // IMPORTANT: Clean up any old temp Firebase files on startup
                CleanupOldTempFiles();
                
                var files = EnvironmentService.Instance.Config.Files;
                GoogleCredential credential = null;

                var encName = files.FirebaseServiceAccountEncryptedFileName;
                var encPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? string.Empty, encName);

                if (File.Exists(encPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Loading encrypted credentials from: {encPath}");
                    
                    var key = EncryptionKeyService.GetOrLoadEncryptionKey();
                    var payload = File.ReadAllBytes(encPath);
                    var jsonBytes = EncryptionService.Decrypt(payload, key);

                    var appTempFolder = Path.Combine(Path.GetTempPath(), "DeliverGate_POS");
                    Directory.CreateDirectory(appTempFolder);
                    
                    var tempPath = Path.Combine(appTempFolder, "firebase-credentials.json");
                    
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Writing decrypted credentials to temp: {tempPath}");
                    
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
                    
                    File.WriteAllBytes(tempPath, jsonBytes);
                    
                    try
                    {
                        credential = GoogleCredential.FromFile(tempPath);
                        System.Diagnostics.Debug.WriteLine("[Firebase] Successfully loaded credentials from temp file");
                    }
                    finally
                    {
                        try 
                        { 
                            File.Delete(tempPath);
                            System.Diagnostics.Debug.WriteLine("[Firebase] Deleted temp credentials file after loading");
                        } 
                        catch (Exception delEx) 
                        { 
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Info: Temp file will be cleaned up later: {delEx.Message}");
                        }
                    }
                }
                else
                {
                    string[] possiblePaths = {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, files.FirebaseServiceAccountFileName),
                        Path.Combine(Directory.GetCurrentDirectory(), files.FirebaseServiceAccountFileName),
                        Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), files.FirebaseServiceAccountFileName),
                        files.FirebaseServiceAccountFileName
                    };
                    string credentialsPath = null;
                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            credentialsPath = path;
                            System.Diagnostics.Debug.WriteLine($"Firebase credentials found at: {path}");
                            break;
                        }
                    }
                    if (string.IsNullOrEmpty(credentialsPath))
                    {
                        System.Diagnostics.Debug.WriteLine("Firebase credentials file not found in any of the expected locations");
                        return;
                    }
                    credential = GoogleCredential.FromFile(credentialsPath);
                }
                
                // Project ID will be extracted from the service account JSON file (the source of truth)
                // Fallback to "delivergate-uk" only if extraction fails (should never happen)
                string resolvedProjectId = "delivergate-uk"; // Default fallback
                try
                {
                    string projectIdFromCreds = null;
                    if (File.Exists(encPath))
                    {
                        var key = EncryptionKeyService.GetOrLoadEncryptionKey();
                        var payload = File.ReadAllBytes(encPath);
                        var jsonBytes = EncryptionService.Decrypt(payload, key);
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonBytes);
                        projectIdFromCreds = doc.RootElement.GetProperty("project_id").GetString();
                    }
                    else
                    {
                        string[] possiblePathsForId = {
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, files.FirebaseServiceAccountFileName),
                            Path.Combine(Directory.GetCurrentDirectory(), files.FirebaseServiceAccountFileName),
                            Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), files.FirebaseServiceAccountFileName),
                            files.FirebaseServiceAccountFileName
                        };
                        foreach (var path in possiblePathsForId)
                        {
                            if (File.Exists(path))
                            {
                                var json = File.ReadAllText(path);
                                projectIdFromCreds = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("project_id").GetString();
                                break;
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(projectIdFromCreds))
                    {
                        resolvedProjectId = projectIdFromCreds;
                        System.Diagnostics.Debug.WriteLine($"[Firebase] Using project_id from service account file: {resolvedProjectId}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Firebase] Warning: Could not extract project_id from service account, using fallback: {resolvedProjectId}");
                    }
                }
                catch (Exception projEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Warning: Error extracting project_id: {projEx.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Using fallback project_id: {resolvedProjectId}");
                }

                // Create Firestore client with explicit credentials
                _firestoreDb = new FirestoreDbBuilder
                {
                    ProjectId = resolvedProjectId,
                    Credential = credential
                }.Build();
                
                System.Diagnostics.Debug.WriteLine($"Firebase initialized successfully with project: {resolvedProjectId}");
                
                lock (_lockObject)
                {
                    _isInitialized = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Firebase initialization error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        public async Task<bool> CheckCollectionExistsAsync(string collectionName)
        {
            await InitializeFirebaseAsync();
            
            try
            {
                if (_firestoreDb == null)
                {
                    return false;
                }

                // Try to get a document from the collection to check if it exists
                CollectionReference collection = _firestoreDb.Collection(collectionName);
                
                // Get the first document in the collection
                QuerySnapshot snapshot = await collection.Limit(1).GetSnapshotAsync();
                
                return snapshot.Count > 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error checking collection '{collectionName}': {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public async Task ShowCollectionAlertAsync(string collectionName)
        {
            try
            {
                bool exists = await CheckCollectionExistsAsync(collectionName);
                
                string message = exists 
                    ? $"Collection '{collectionName}' exists in Firebase!" 
                    : $"Collection '{collectionName}' does not exist in Firebase.";
                
                string title = exists ? "Collection Found" : "Collection Not Found";
                MessageBoxImage icon = exists ? MessageBoxImage.Information : MessageBoxImage.Warning;
                
                MessageBox.Show(message, title, MessageBoxButton.OK, icon);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing collection alert: {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<List<string>> GetAllCollectionsAsync()
        {
            await InitializeFirebaseAsync();
            
            try
            {
                if (_firestoreDb == null)
                {
                    return new List<string>();
                }

                var collections = new List<string>();
                var rootCollections = _firestoreDb.ListRootCollectionsAsync();
                
                await foreach (var collection in rootCollections)
                {
                    collections.Add(collection.Id);
                }
                
                return collections;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting all collections: {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return new List<string>();
            }
        }

        public async Task ShowAllCollectionsAsync()
        {
            try
            {
                var collections = await GetAllCollectionsAsync();
                
                if (collections.Count == 0)
                {
                    MessageBox.Show("No collections found in Firebase.", "Collections", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                string collectionsList = string.Join("\n", collections.Select((name, index) => $"{index + 1}. {name}"));
                string message = $"Found {collections.Count} collection(s) in Firebase:\n\n{collectionsList}";
                
                MessageBox.Show(message, "All Collections", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing all collections: {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task<QuerySnapshot> GetCollectionDocumentsAsync(string collectionName)
        {
            await InitializeFirebaseAsync();
            
            try
            {
                if (_firestoreDb == null)
                {
                    return null;
                }

                CollectionReference collection = _firestoreDb.Collection(collectionName);
                return await collection.GetSnapshotAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting documents from collection '{collectionName}': {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        public async Task ShowCollectionDocumentsAsync(string collectionName)
        {
            try
            {
                var snapshot = await GetCollectionDocumentsAsync(collectionName);
                
                if (snapshot == null || snapshot.Count == 0)
                {
                    MessageBox.Show($"No documents found in collection '{collectionName}'.", "Documents", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                var documentInfo = new List<string>();
                foreach (var doc in snapshot.Documents)
                {
                    documentInfo.Add($"Document ID: {doc.Id}");
                    if (doc.Exists)
                    {
                        var data = doc.ToDictionary();
                        foreach (var field in data)
                        {
                            documentInfo.Add($"  {field.Key}: {field.Value}");
                        }
                    }
                    documentInfo.Add(""); // Empty line for separation
                }
                
                string documentsList = string.Join("\n", documentInfo);
                string message = $"Found {snapshot.Count} document(s) in collection '{collectionName}':\n\n{documentsList}";
                
                MessageBox.Show(message, $"Documents in {collectionName}", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error showing collection documents: {ex.Message}", "Firebase Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task TestFirebaseConnectionAsync()
        {
            await InitializeFirebaseAsync();
            
            try
            {
                if (_firestoreDb == null)
                {
                    MessageBox.Show("Firebase not initialized. Please check your credentials file.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Try to list collections to test connection
                var collections = _firestoreDb.ListRootCollectionsAsync();
                var collectionList = new List<string>();
                int count = 0;
                await foreach (var collection in collections)
                {
                    collectionList.Add(collection.Id);
                    count++;
                }
                
                if (count == 0)
                {
                    string message = "Firebase connection successful! No root collections found.";
                    MessageBox.Show(message, "Firebase Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string collectionsList = string.Join("\n", collectionList.Select((name, index) => $"{index + 1}. {name}"));
                    string message = $"Firebase connection successful! Found {count} root collection(s):\n\n{collectionsList}";
                    MessageBox.Show(message, "Firebase Connection Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Firebase connection failed: {ex.Message}", "Firebase Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public async Task StartListeningToCollectionAsync(string collectionName = null)
        {
            await InitializeFirebaseAsync();

            if (_isListening)
            {
                await StopListeningToCollectionAsync();
            }

            if (_firestoreDb == null)
            {
                System.Diagnostics.Debug.WriteLine("[Firebase] Cannot start listener - Firestore DB not initialized");
                return;
            }

            // Use collection name from settings if not provided
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                collectionName = GetCollectionNameFromSettings();
            }

            _activeCollectionName = collectionName;
            _listenerCancellationTokenSource = new CancellationTokenSource();
            _isListening = true;
            _reconnectAttempts = 0;
            
            System.Diagnostics.Debug.WriteLine($"[Firebase] Starting listener for collection: {collectionName}");
            
            // Start health check timer (check every 5 minutes)
            _healthCheckTimer?.Dispose();
            _healthCheckTimer = new Timer(async _ => await CheckListenerHealthAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            
            // Start the listener in a background task with auto-reconnect
            _ = Task.Run(async () => await StartListenerWithRetryAsync(), _listenerCancellationTokenSource.Token);
        }
        
        private async Task StartListenerWithRetryAsync()
        {
            while (!_listenerCancellationTokenSource.Token.IsCancellationRequested && _reconnectAttempts < MaxReconnectAttempts)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Listener attempt #{_reconnectAttempts + 1} for collection: {_activeCollectionName}");
                    
                    _firestoreListener = _firestoreDb.Collection(_activeCollectionName).Listen(snapshot =>
                    {
                        // Check if we should stop listening
                        if (_listenerCancellationTokenSource.Token.IsCancellationRequested)
                            return;

                        try
                        {
                            _lastSuccessfulConnection = DateTime.Now;
                            _reconnectAttempts = 0; // Reset on successful callback
                            
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Received snapshot with {snapshot.Changes.Count()} changes");
                            
                            foreach (var change in snapshot.Changes)
                            {
                                string changeType = change.ChangeType.ToString();
                                string documentId = change.Document.Id;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    OnCollectionChanged?.Invoke(changeType, documentId);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Firebase] Error processing snapshot changes: {ex.Message}");
                        }
                    });

                    System.Diagnostics.Debug.WriteLine($"[Firebase] Listener started successfully at {DateTime.Now}");
                    _lastSuccessfulConnection = DateTime.Now;
                    _reconnectAttempts = 0;

                    // Keep the task alive until cancellation is requested
                    while (!_listenerCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, _listenerCancellationTokenSource.Token);
                    }
                    
                    // If we reach here, cancellation was requested (clean exit)
                    System.Diagnostics.Debug.WriteLine("[Firebase] Listener stopped cleanly (cancellation requested)");
                    break;
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("[Firebase] Listener cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _reconnectAttempts++;
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Listener error (attempt {_reconnectAttempts}/{MaxReconnectAttempts}): {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Firebase] Stack trace: {ex.StackTrace}");
                    
                    // Clean up failed listener
                    try
                    {
                        _firestoreListener = null;
                    }
                    catch { }
                    
                    if (_reconnectAttempts < MaxReconnectAttempts && !_listenerCancellationTokenSource.Token.IsCancellationRequested)
                    {
                        // Exponential backoff: 2, 4, 8, 16, 32, 60, 60, 60... seconds
                        var delaySeconds = Math.Min(60, Math.Pow(2, _reconnectAttempts));
                        System.Diagnostics.Debug.WriteLine($"[Firebase] Reconnecting in {delaySeconds} seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), _listenerCancellationTokenSource.Token);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Firebase] Max reconnect attempts reached or cancelled - stopping listener");
                        _isListening = false;
                        break;
                    }
                }
            }
        }
        
        private async Task CheckListenerHealthAsync()
        {
            try
            {
                if (!_isListening)
                {
                    System.Diagnostics.Debug.WriteLine("[Firebase] Health check: Listener not running");
                    return;
                }
                
                var timeSinceLastConnection = DateTime.Now - _lastSuccessfulConnection;
                System.Diagnostics.Debug.WriteLine($"[Firebase] Health check: Last successful connection was {timeSinceLastConnection.TotalMinutes:F1} minutes ago");
                
                // If no successful connection in the last 10 minutes and we're supposed to be listening, restart
                if (timeSinceLastConnection.TotalMinutes > 10 && _reconnectAttempts < MaxReconnectAttempts)
                {
                    System.Diagnostics.Debug.WriteLine("[Firebase] Health check: Restarting listener due to inactivity");
                    await StopListeningToCollectionAsync();
                    await StartListeningToCollectionAsync(_activeCollectionName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] Health check error: {ex.Message}");
            }
        }

        public async Task StopListeningToCollectionAsync()
        {
            if (!_isListening)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("[Firebase] Stopping listener...");
                
                // Stop health check timer
                _healthCheckTimer?.Dispose();
                _healthCheckTimer = null;
                
                // Cancel the listener task
                _listenerCancellationTokenSource?.Cancel();
                _firestoreListener = null;
                _isListening = false;
                _activeCollectionName = null;
                
                System.Diagnostics.Debug.WriteLine("[Firebase] Listener stopped successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Firebase] Error stopping listener: {ex.Message}");
            }
        }

        public bool IsListening => _isListening;
        
        public bool IsInitialized => _isInitialized;
        
        public bool IsFirebaseAvailable => _firestoreDb != null;
    }
} 