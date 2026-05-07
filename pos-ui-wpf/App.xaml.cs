using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Collections.ObjectModel;
using POS_UI.Models;
using POS_UI.Services;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Windows.Threading;
using System.Windows.Input;
using System.Diagnostics;

namespace POS_UI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string SingleInstanceMutexName = "Global\\POS_UI_SingleInstance_B7E3F2A1";

        [Flags]
        private enum EXECUTION_STATE : uint
        {
            ES_AWAYMODE_REQUIRED = 0x00000040,
            ES_CONTINUOUS        = 0x80000000,
            ES_DISPLAY_REQUIRED  = 0x00000002,
            ES_SYSTEM_REQUIRED   = 0x00000001
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern EXECUTION_STATE SetThreadExecutionState(EXECUTION_STATE esFlags);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        private static Mutex? _singleInstanceMutex;
        private DispatcherTimer? _memoryCleanupTimer;
        private DispatcherTimer? _fourAmLogoutTimer;
        private DispatcherTimer? _idleLogoutTimer;
        private DateTime _lastActivityTime = DateTime.Now;
        private Point _lastTrackedMousePosition = new Point(double.NaN, double.NaN);
        private int _gpuMemoryErrorCount = 0;
        private bool _hasAutoSwitchedToSoftwareRendering = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                ActivateExistingInstance();
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // CRITICAL: Configure WPF rendering for optimal performance with GPU memory protection
            try
            {
                // Use HARDWARE rendering for smooth performance
                // We'll handle GPU memory issues with aggressive cleanup and monitoring
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
                
                // Performance optimizations: Set ideal text rendering globally
                System.Windows.Media.TextOptions.TextFormattingModeProperty.OverrideMetadata(
                    typeof(System.Windows.FrameworkElement),
                    new System.Windows.FrameworkPropertyMetadata(System.Windows.Media.TextFormattingMode.Ideal));
            }
            catch { }
            
            // Optimize dispatcher priority for better UI responsiveness
            try
            {
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Input);
            }
            catch { }
            
            // Enable touch scrolling for POS system
            try
            {
                // Disable Windows touch gestures that interfere with scrolling
                EventManager.RegisterClassHandler(typeof(System.Windows.UIElement), 
                    System.Windows.UIElement.PreviewStylusSystemGestureEvent, 
                    new System.Windows.Input.StylusSystemGestureEventHandler((sender, args) =>
                    {
                        // Allow only touch down/up, block press-and-hold and flicks
                        if (args.SystemGesture != System.Windows.Input.SystemGesture.Tap &&
                            args.SystemGesture != System.Windows.Input.SystemGesture.Drag)
                        {
                            args.Handled = true;
                        }
                    }));
                
                System.Diagnostics.Debug.WriteLine("[App] Touch scrolling enabled globally");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Touch setup failed: {ex.Message}");
            }

            // Prevent on-screen keyboard from appearing on DatePicker text boxes
            try
            {
                EventManager.RegisterClassHandler(
                    typeof(System.Windows.Controls.Primitives.DatePickerTextBox),
                    System.Windows.UIElement.GotKeyboardFocusEvent,
                    new System.Windows.Input.KeyboardFocusChangedEventHandler((sender, args) =>
                    {
                        if (sender is System.Windows.Controls.Primitives.DatePickerTextBox dpTextBox)
                        {
                            System.Windows.Input.Keyboard.ClearFocus();
                            args.Handled = true;
                        }
                    }));
            }
            catch { }

            // Dismiss on-screen keyboard when Enter is pressed on any TextBox
            try
            {
                EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                    System.Windows.UIElement.KeyDownEvent,
                    new System.Windows.Input.KeyEventHandler((sender, args) =>
                    {
                        if (args.Key == System.Windows.Input.Key.Enter &&
                            sender is System.Windows.Controls.TextBox tb &&
                            !tb.AcceptsReturn)
                        {
                            tb.MoveFocus(new System.Windows.Input.TraversalRequest(
                                System.Windows.Input.FocusNavigationDirection.Next));
                            args.Handled = true;
                        }
                    }));
            }
            catch { }

            // Configure GC for server workload (better for long-running POS app with large memory)
            try
            {
                System.Runtime.GCSettings.LatencyMode = System.Runtime.GCLatencyMode.Batch;
                if (Environment.Is64BitProcess)
                {
                    // Enable large object heap compaction for 64-bit processes
                    System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                }
            }
            catch { }

            // Log application startup diagnostics
            try
            {
                var bitness = Environment.Is64BitProcess ? "64-bit" : "32-bit";
                var renderingTier = (System.Windows.Media.RenderCapability.Tier >> 16);
                var gcMode = System.Runtime.GCSettings.IsServerGC ? "ServerGC" : "WorkstationGC";
                
                // Production logging: Write essential info to log file
                var startupInfo = $"POS Started | {bitness} | GPU-Tier:{renderingTier} | {gcMode} | PID:{Environment.ProcessId}";
                try { POS_UI.Services.LogService.Info(startupInfo); } catch { }
                
#if DEBUG
                // Debug mode: Detailed console output for development
                var memoryLimit = Environment.Is64BitProcess ? "Large Address Space (>2GB)" : "Limited to ~2GB (32-bit)";
                var pointerSize = IntPtr.Size * 8;
                var osArch = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                var latencyMode = System.Runtime.GCSettings.LatencyMode.ToString();
                var renderingMode = System.Windows.Media.RenderOptions.ProcessRenderMode.ToString();
                var gpuInfo = renderingTier switch
                {
                    0 => "No GPU",
                    1 => "Partial GPU",
                    2 => "Full GPU",
                    _ => "Unknown"
                };
                
                System.Diagnostics.Debug.WriteLine("=== APPLICATION STARTUP DIAGNOSTIC ===");
                System.Diagnostics.Debug.WriteLine($"Mode: {bitness} ({pointerSize}-bit pointers)");
                System.Diagnostics.Debug.WriteLine($"Memory: {memoryLimit}");
                System.Diagnostics.Debug.WriteLine($"OS: {osArch}");
                System.Diagnostics.Debug.WriteLine($"Rendering: {renderingMode} (GPU Tier {renderingTier} - {gpuInfo})");
                System.Diagnostics.Debug.WriteLine($"GC: {gcMode} ({latencyMode})");
                System.Diagnostics.Debug.WriteLine($"Initial Memory: {GetMemoryDiagnostics()}");
                System.Diagnostics.Debug.WriteLine($"PID: {Environment.ProcessId}");
                System.Diagnostics.Debug.WriteLine("=====================================");
                
                Console.WriteLine($"\n=== POS Application - {bitness} Mode ===");
                Console.WriteLine($"Memory: {memoryLimit}");
                Console.WriteLine($"Rendering: {renderingMode} (GPU Tier {renderingTier})");
                Console.WriteLine($"GC: {gcMode} ({latencyMode})");
                Console.WriteLine($"GPU Memory Protection: Enabled (cleanup every 3 min)\n");
#endif
            }
            catch { }

            // Setup aggressive periodic GPU memory cleanup timer (runs every 3 minutes for hardware rendering)
            try
            {
                _memoryCleanupTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(3) // More frequent cleanup for GPU memory
                };
                _memoryCleanupTimer.Tick += (s, args) =>
                {
                    try
                    {
                        var beforeCleanup = GetMemoryDiagnostics();
                        
                        // AGGRESSIVE MEMORY CLEANUP (GPU + System Memory)
                        // 1. Flush WPF rendering pipeline (GPU memory)
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.SystemIdle);
                        
                        // 2. Compact Large Object Heap to reduce fragmentation (System memory)
                        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
                        
                        // 3. AGGRESSIVE garbage collection (System + GPU memory)
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                        GC.WaitForPendingFinalizers();
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                        GC.WaitForPendingFinalizers();
                        
                        // 4. Final compacting collection
                        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                        
                        var afterCleanup = GetMemoryDiagnostics();
                        
#if DEBUG
                        // Debug mode: Log before/after stats
                        System.Diagnostics.Debug.WriteLine($"[Memory Cleanup] BEFORE: {beforeCleanup}");
                        System.Diagnostics.Debug.WriteLine($"[Memory Cleanup] AFTER:  {afterCleanup}");
                        try { POS_UI.Services.LogService.Info($"Memory Cleanup | Before: {beforeCleanup} | After: {afterCleanup}"); } catch { }
#else
                        // Release mode: Just log the after state
                        try { POS_UI.Services.LogService.Info($"Memory Cleanup: {afterCleanup}"); } catch { }
#endif
                    }
                    catch { }
                };
                _memoryCleanupTimer.Start();
#if DEBUG
                Console.WriteLine("GPU memory cleanup timer started (every 3 minutes)");
#endif
            }
            catch { }

            // 4:00 AM auto-logout: check every minute and logout when time is 4:00 AM
            try
            {
                _fourAmLogoutTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(1)
                };
                _fourAmLogoutTimer.Tick += (s, args) =>
                {
                    try
                    {
                        var now = DateTime.Now;
                        if (now.Hour == 4 && now.Minute == 0)
                        {
                            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                System.Diagnostics.Debug.WriteLine("[App] 4:00 AM auto-logout triggered.");
                                new TokenValidationService().LogoutAndNavigateToLogin("4:00 AM auto-logout");
                            }
                        }
                    }
                    catch { }
                };
                _fourAmLogoutTimer.Start();
            }
            catch { }

            // Idle logout after 10 minutes: track input and logout when idle
            try
            {
                _lastActivityTime = DateTime.Now;
                InputManager.Current.PreProcessInput += OnApplicationInput;
                _idleLogoutTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMinutes(1)
                };
                _idleLogoutTimer.Tick += (s, args) =>
                {
                    try
                    {
                        var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                        if (string.IsNullOrEmpty(accessToken)) return;
                        var idleLogoutMins = 10;
                        try { idleLogoutMins = Math.Max(1, Math.Min(120, GlobalDataService.Instance?.IdleLogoutMinutes ?? 10)); } catch { }
                        var idleMinutes = (DateTime.Now - _lastActivityTime).TotalMinutes;
                        if (idleMinutes >= idleLogoutMins)
                        {
                            System.Diagnostics.Debug.WriteLine($"[App] Idle logout triggered after {idleLogoutMins} minutes.");
                            new TokenValidationService().LogoutAndNavigateToLogin($"Idle logout ({idleLogoutMins} minutes)");
                        }
                    }
                    catch { }
                };
                _idleLogoutTimer.Start();
            }
            catch { }

            // Initialize environment configuration
            try
            {
                EnvironmentService.Instance.Initialize();
#if DEBUG
                Console.WriteLine($"POS Environment: {EnvironmentService.Instance.EnvironmentName}");
#endif
            }
            catch (Exception ex)
            {
                // Log critical initialization failure
                try { POS_UI.Services.LogService.Error("Environment initialization failed", ex); } catch { }
            }

            // Ensure encryption key is available (USB key on first run or every run per config)
            try
            {
                var requireEveryRun = EncryptionKeyService.RequireUsbThisRun();
                if (requireEveryRun)
                {
                    // Always load from USB key; do not rely on stored credential
                    var cfg = EnvironmentService.Instance.Config.Security;
                    var key = EncryptionKeyService.LoadFromUsbKeyFile(cfg.UsbKeyFileName);
                    if (key == null || key.Length != 32)
                    {
                        MessageBox.Show("Encryption key not found. Insert USB with Key", "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                        Current.Shutdown();
                        return;
                    }
                }
                else
                {
                    // First run will load from USB and persist to Credential Manager
                    _ = EncryptionKeyService.GetOrLoadEncryptionKey();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Security", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
                return;
            }

            // Ensure base folder on Desktop exists and migrate existing files/folders
            try { PathService.EnsureInitialized(); } catch { }

            // Global exception handlers to surface ALL errors to the user
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                var ex = exArgs.Exception;
                if (ex is TaskCanceledException || ex is OperationCanceledException || IsBenignCancellation(ex))
                {
                    // Suppress inactivity/timeouts silently
                    exArgs.Handled = true;
                    return;
                }
                
                // Special handling for OutOfMemoryException
                if (ex is OutOfMemoryException)
                {
                    try
                    {
                        var memInfo = GetMemoryDiagnostics();
                        var isGpuMemoryIssue = ex.StackTrace?.Contains("DUCE.Channel") == true || 
                                               ex.StackTrace?.Contains("MediaContext") == true;
                        
                        var errorType = isGpuMemoryIssue ? "GPU/RENDERING MEMORY" : "SYSTEM MEMORY";
                        POS_UI.Services.LogService.Error($"OUT OF MEMORY EXCEPTION ({errorType}) - {memInfo}", ex);
                        
                        // Track GPU memory errors
                        if (isGpuMemoryIssue)
                        {
                            _gpuMemoryErrorCount++;
                            
                            // Auto-switch to software rendering if GPU memory errors are frequent
                            if (_gpuMemoryErrorCount >= 3 && !_hasAutoSwitchedToSoftwareRendering)
                            {
                                _hasAutoSwitchedToSoftwareRendering = true;
                                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
                                POS_UI.Services.LogService.Error("AUTO-SWITCHED TO SOFTWARE RENDERING due to repeated GPU memory errors", null);
                                
                                MessageBox.Show(
                                    "The application has automatically switched to Software Rendering mode due to repeated GPU memory issues.\n\n" +
                                    "This will prevent crashes but rendering may be slightly slower.\n\n" +
                                    "The application will continue running normally.",
                                    "Rendering Mode Changed",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            }
                        }
                        
                        // Emergency cleanup: flush WPF rendering and free memory
                        try
                        {
                            Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.SystemIdle);
                        }
                        catch { }
                        
                        // ULTRA-AGGRESSIVE garbage collection for GPU memory recovery
                        for (int i = 0; i < 3; i++)
                        {
                            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
                            GC.WaitForPendingFinalizers();
                        }
                        
                        var recoveryMsg = isGpuMemoryIssue 
                            ? $"GPU/Rendering memory exhausted (Error #{_gpuMemoryErrorCount}). Flushed rendering pipeline and freed resources."
                            : "System memory low. Forced garbage collection.";
                        
                        ShowGlobalError("Memory Error - Recovery Attempted", 
                            new Exception($"{recoveryMsg}\n\n{memInfo}\n\nThe application will continue running.\n\nTechnical: {ex.Message}"));
                    }
                    catch { }
                    exArgs.Handled = true;
                    return;
                }
                
                try { POS_UI.Services.LogService.Error("DispatcherUnhandledException", ex); } catch { }
                ShowGlobalError("Unhandled UI Exception", ex);
                exArgs.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                var ex = exArgs.ExceptionObject as Exception;
                if (ex is TaskCanceledException || ex is OperationCanceledException || IsBenignCancellation(ex)) { return; }
                try { POS_UI.Services.LogService.Error("Unhandled Domain Exception", ex); } catch { }
                ShowGlobalError("Unhandled Domain Exception", ex ?? new Exception("Unknown domain exception"));
            };
            TaskScheduler.UnobservedTaskException += (s, exArgs) =>
            {
                // Suppress ALL unobserved task exceptions to avoid UI popups from finalizer thread
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[UnobservedTaskException] Suppressed: {exArgs.Exception?.GetType().Name}: {exArgs.Exception?.Message}");
                    try { POS_UI.Services.LogService.Warn($"UnobservedTaskException: {exArgs.Exception?.GetType().Name}: {exArgs.Exception?.Message}"); } catch { }
                }
                catch { }
                exArgs.SetObserved();
                return;
            };

            // Set the console output to the output window
            //AllocConsole(); 
            // Load printers ONCE at app startup
            PrintersService.Instance.GetConnectedPrinters();

            // Load printer settings ONCE at app startup
            PrinterSettingsService.Instance.Refresh();

            // Load card machines ONCE at app startup
            CardMachineService.Instance.Refresh();

            // Load global data from storage if available
            var globalDataService = GlobalDataService.Instance;
            globalDataService.LoadDataFromStorage();

            // Initialize network connectivity monitoring
            var networkService = NetworkConnectivityService.Instance;
            networkService.StartMonitoring();

            // Prevent system and display from sleeping while app is running
            try { SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS | EXECUTION_STATE.ES_SYSTEM_REQUIRED | EXECUTION_STATE.ES_DISPLAY_REQUIRED); } catch { }

            // Apply window/taskbar icon from remote URL once the main window is created
            try
            {
                // Defer until UI is idle to ensure windows are created
                this.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _ = ApplyRemoteIconToAllWindowsAsync("https://delivergate-logos.s3.eu-west-2.amazonaws.com/POSPNG.png");

                    // Also update future windows that may be opened later in the session
                    this.Activated += async (_, __) =>
                    {
                        await ApplyRemoteIconToAllWindowsAsync("https://delivergate-logos.s3.eu-west-2.amazonaws.com/POSPNG.png");
                    };
                }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            catch { }
        }

        private void OnApplicationInput(object sender, PreProcessInputEventArgs e)
        {
            var input = e.StagingItem.Input;

            if (input is TouchEventArgs)
            {
                // Actual finger contact on the touch screen
            }
            else if (input is StylusEventArgs stylus)
            {
                // Ignore hover/proximity events from the digitizer
                if (stylus.InAir) return;
            }
            else if (input is MouseEventArgs)
            {
                // WPF promotes touch to mouse events and also generates synthetic
                // MouseMove when the visual tree updates under the cursor; only
                // count if the pointer actually moved to a new position.
                try
                {
                    var win = Application.Current?.MainWindow;
                    if (win == null) return;
                    var pos = Mouse.GetPosition(win);
                    if (pos == _lastTrackedMousePosition) return;
                    _lastTrackedMousePosition = pos;
                }
                catch { return; }
            }
            else if (input is KeyboardEventArgs)
            {
                // Physical or on-screen keyboard
            }
            else
            {
                return;
            }

            _lastActivityTime = DateTime.Now;
        }

        private void ShowGlobalError(string title, Exception ex)
        {
            try
            {
                var message = ex?.Message ?? "Unknown error";
                if (ex?.InnerException != null)
                {
                    message += "\n\nInner: " + ex.InnerException.Message;
                }
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            catch
            {
                // last resort
            }
        }

        private static async Task ApplyRemoteIconToAllWindowsAsync(string imageUrl)
        {
            try
            {
                var iconImage = await DownloadImageAsync(imageUrl);
                if (iconImage == null) return;

                foreach (Window window in Current.Windows)
                {
                    try { window.Icon = iconImage; } catch { }
                }
            }
            catch { }
        }

        private static async Task<ImageSource> DownloadImageAsync(string imageUrl)
        {
            try
            {
                using (var http = new HttpClient())
                {
                    var bytes = await http.GetByteArrayAsync(imageUrl);
                    using (var ms = new MemoryStream(bytes))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = ms;
                        bitmap.DecodePixelWidth = 256; // reasonable taskbar/title size
                        bitmap.EndInit();
                        bitmap.Freeze();
                        return bitmap;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool IsBenignCancellation(Exception ex)
        {
            if (ex == null) return false;
            var msg = ex.Message?.ToLowerInvariant() ?? string.Empty;
            // Common benign cancellation/aborted-operation messages from HttpClient/WPF shutdown
            if (msg.Contains("a task was canceled") || msg.Contains("task was cancelled")) return true;
            if (msg.Contains("the operation was canceled") || msg.Contains("operation canceled")) return true;
            if (msg.Contains("operation aborted") || msg.Contains("request aborted")) return true;
            // MaterialDesign DialogHost can throw when closing/opening in quick succession; flow still works
            if (msg.Contains("already open") || msg.Contains("dialoghost")) return true;
            if (ex.InnerException != null) return IsBenignCancellation(ex.InnerException);
            return false;
        }

        private static string GetMemoryDiagnostics()
        {
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var workingSetMB = process.WorkingSet64 / 1024 / 1024;
                var privateMemoryMB = process.PrivateMemorySize64 / 1024 / 1024;
                var virtualMemoryMB = process.VirtualMemorySize64 / 1024 / 1024;
                var gcMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
                var gen0 = GC.CollectionCount(0);
                var gen1 = GC.CollectionCount(1);
                var gen2 = GC.CollectionCount(2);
                
                return $"WorkingSet: {workingSetMB}MB | Private: {privateMemoryMB}MB | Virtual: {virtualMemoryMB}MB | " +
                       $"GC Memory: {gcMemoryMB}MB | Collections (G0/G1/G2): {gen0}/{gen1}/{gen2}";
            }
            catch
            {
                return "Unable to get memory diagnostics";
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Restore normal execution state (allow sleep)
                try { SetThreadExecutionState(EXECUTION_STATE.ES_CONTINUOUS); } catch { }

                // Persist cashier cart as ongoing_order when temp payments exist (same as sidebar logout), while tokens still work.
                // Must run off the UI thread: OnExit is on the dispatcher, and blocking with GetResult() while ApiService HTTP awaits
                // capture DispatcherSynchronizationContext deadlocks (continuations never run).
                try
                {
                    Task.Run(() => OngoingOrderConfigPersistence.TrySaveFromCartAsync().GetAwaiter().GetResult())
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] OnExit ongoing order persist: {ex}");
                }

                // Clear all draft orders when application exits
                var draftStorageService = new DraftStorageService();
                draftStorageService.ClearAllDrafts();
            }
            catch { }

            // Clear auth/session-related local storage (logout on close)
            try
            {
                var tokenValidationService = new TokenValidationService();
                tokenValidationService.ClearTokens();

                var localStorageService = new LocalStorageService();
                localStorageService.ClearAllData();

                var navigationStateService = new NavigationStateService();
                navigationStateService.ClearNavigationState();
            }
            catch { }

            try
            {
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var processes = Process.GetProcessesByName(currentProcess.ProcessName);

                foreach (var process in processes)
                {
                    if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                    {
                        if (IsIconic(process.MainWindowHandle))
                            ShowWindow(process.MainWindowHandle, SW_RESTORE);

                        SetForegroundWindow(process.MainWindowHandle);
                        break;
                    }
                }
            }
            catch { }

            MessageBox.Show(
                "POS application is already running.",
                "POS",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
