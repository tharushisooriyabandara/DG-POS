using System;
using System.Text.Json;
using POS_UI.Properties;

namespace POS_UI.Services
{
    public class NavigationStateService
    {
        private const string NavigationStateKey = "NavigationState";
        private const string NavigationDataKey = "NavigationData";

        public class NavigationState
        {
            public string PageUri { get; set; }
            public string PageType { get; set; }
            public string NavigationData { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public void SaveNavigationState(string pageUri, string pageType = null, object navigationData = null)
        {
            try
            {
                var state = new NavigationState
                {
                    PageUri = pageUri,
                    PageType = pageType,
                    NavigationData = navigationData != null ? JsonSerializer.Serialize(navigationData) : null,
                    Timestamp = DateTime.Now
                };

                var json = JsonSerializer.Serialize(state);
                Settings.Default[NavigationStateKey] = json;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Silent error - navigation state is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to save navigation state: {ex.Message}");
            }
        }

        public NavigationState GetNavigationState()
        {
            try
            {
                var json = Settings.Default[NavigationStateKey] as string;
                if (string.IsNullOrEmpty(json))
                    return null;
                
                return JsonSerializer.Deserialize<NavigationState>(json);
            }
            catch (Exception ex)
            {
                // Silent error - navigation state is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to retrieve navigation state: {ex.Message}");
                return null;
            }
        }

        public void ClearNavigationState()
        {
            try
            {
                Settings.Default[NavigationStateKey] = null;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                // Silent error - navigation state is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to clear navigation state: {ex.Message}");
            }
        }

        public bool HasNavigationState()
        {
            return GetNavigationState() != null;
        }

        public T GetNavigationData<T>()
        {
            try 
            {
                var state = GetNavigationState();
                if (state?.NavigationData == null)
                    return default(T);

                return JsonSerializer.Deserialize<T>(state.NavigationData);
            }
            catch (Exception ex)
            {
                // Silent error - navigation state is not critical
                System.Diagnostics.Debug.WriteLine($"Failed to deserialize navigation data: {ex.Message}");
                return default(T);
            }
        }
    }
}