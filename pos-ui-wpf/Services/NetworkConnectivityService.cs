using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;

namespace POS_UI.Services
{
    public class NetworkConnectivityService
    {
        private static NetworkConnectivityService _instance;
        private static readonly object _lock = new object();

        private readonly HttpClient _httpClient;
        private readonly System.Timers.Timer _connectivityTimer;
        private bool _isConnected;

        public static NetworkConnectivityService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new NetworkConnectivityService();
                        }
                    } 
                }
                return _instance;
            }
        }

        private NetworkConnectivityService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(2);

            _connectivityTimer = new System.Timers.Timer(10000);
            _connectivityTimer.Elapsed += async (sender, e) => await CheckConnectivityAsync();

            _ = Task.Run(async () => await CheckConnectivityAsync());
        }

        public bool IsConnected => _isConnected;

        public event EventHandler<bool> ConnectivityChanged;

        public async Task<bool> CheckConnectivityAsync()
        {
            try
            {
                // Try to connect to a reliable service
                var response = await _httpClient.GetAsync("https://clients3.google.com/generate_204");
                var wasConnected = _isConnected;
                _isConnected = response.IsSuccessStatusCode;

                if (wasConnected != _isConnected)
                {
                    ConnectivityChanged?.Invoke(this, _isConnected);
                }
                return _isConnected;
            }
            catch
            {
                var wasConnected = _isConnected;
                _isConnected = false;

                if (wasConnected != _isConnected)
                {
                    ConnectivityChanged?.Invoke(this, _isConnected);
                }
                return false;
            }
        }

        public void StartMonitoring()
        {
            _connectivityTimer.Start();
        }

        public void StopMonitoring()
        {
            _connectivityTimer.Stop();
        }

        public void Dispose()
        {
            _connectivityTimer.Dispose();
            _httpClient?.Dispose();
        }
    }
}