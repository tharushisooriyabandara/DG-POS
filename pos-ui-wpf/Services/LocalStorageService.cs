using System;
using System.Text.Json;
using POS_UI.Models;
using POS_UI.Properties;

namespace POS_UI.Services
{
    public class LocalStorageService
    {
        private const string CurrentUserKey = "CurrentUser";
        private const string ShopDetailsKey = "ShopDetails";

        public void SaveCurrentUser(CurrentUserModel user)
        {
            try
            {
                var json = JsonSerializer.Serialize(user);
                Settings.Default[CurrentUserKey] = json;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save current user to local storage: {ex.Message}");
            }
        }

        public CurrentUserModel GetCurrentUser()
        {
            try
            {
                var json = Settings.Default[CurrentUserKey] as string;
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<CurrentUserModel>(json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve current user from local storage: {ex.Message}");
            }
        }

        public void SaveShopDetails(ShopModel shop)
        {
            try
            {
                var json = JsonSerializer.Serialize(shop);
                Settings.Default[ShopDetailsKey] = json;
                Settings.Default.Save();
                System.Diagnostics.Debug.WriteLine($"[LocalStorage] Saved ShopDetails: Id={shop?.Id}, Name={shop?.Name}, CountryCode={shop?.CountryCode}, Lat={shop?.Latitude}, Lng={shop?.Longitude}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save shop details to local storage: {ex.Message}");
            }
        }

        public ShopModel GetShopDetails()
        {
            try
            {
                var json = Settings.Default[ShopDetailsKey] as string;
                if (string.IsNullOrEmpty(json))
                    return null;

                var shop = JsonSerializer.Deserialize<ShopModel>(json);
                System.Diagnostics.Debug.WriteLine($"[LocalStorage] Loaded ShopDetails: Id={shop?.Id}, Name={shop?.Name}, CountryCode={shop?.CountryCode}, Lat={shop?.Latitude}, Lng={shop?.Longitude}");
                return shop;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to retrieve shop details from local storage: {ex.Message}");
            }
        }

        public void ClearAllData()
        {
            try
            {
                Settings.Default[CurrentUserKey] = null;
                Settings.Default[ShopDetailsKey] = null;
                Settings.Default.Save();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to clear local storage data: {ex.Message}");
            }
        }

        public bool HasCurrentUser()
        {
            return GetCurrentUser() != null;
        }

        public bool HasShopDetails()
        {
            return GetShopDetails() != null;
        }
    }
} 