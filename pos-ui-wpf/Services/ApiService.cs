using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using POS_UI.Services;
using System.Diagnostics;
using POS_UI.Models;
using System.Globalization;


namespace POS_UI.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly SettingsService _settingsService;
        
        // OPTIMIZATION: Shared HttpClient for reporting API calls to avoid socket exhaustion
        // and DNS resolution delays from creating new HttpClient on every Z-report call
        private static readonly HttpClient _reportingHttpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        
        static ApiService()
        {
            _reportingHttpClient.DefaultRequestHeaders.Accept.Clear();
            _reportingHttpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public ApiService()
        {
            _httpClient = new HttpClient();
            var baseUrl = EnvironmentService.Instance.Config.Urls.GoApiBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://pos-go-api-dev.delivergate.com";
            }
            _httpClient.BaseAddress = new Uri(baseUrl);
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            // receive responses in JSON format
            _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            
            _settingsService = new SettingsService();
            var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
            _httpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
            _httpClient.DefaultRequestHeaders.Add("x-outlet-code", outletCode);
            if (!string.IsNullOrWhiteSpace(brandId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-brand-id", brandId);
            }
        }

        /// <summary>
        /// Refreshes tenant/outlet/brand headers from current Settings before making API calls.
        /// Ensures first-run after install has correct headers even if this service was constructed early.
        /// </summary>
        public void RefreshHeadersFromSettings()
        {
            var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();

            if (_httpClient.DefaultRequestHeaders.Contains("x-tenant-code"))
            {
                _httpClient.DefaultRequestHeaders.Remove("x-tenant-code");
            }
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                _httpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
            }

            if (_httpClient.DefaultRequestHeaders.Contains("x-outlet-code"))
            {
                _httpClient.DefaultRequestHeaders.Remove("x-outlet-code");
            }
            if (!string.IsNullOrWhiteSpace(outletCode))
            {
                _httpClient.DefaultRequestHeaders.Add("x-outlet-code", outletCode);
            }

            if (_httpClient.DefaultRequestHeaders.Contains("x-brand-id"))
            {
                _httpClient.DefaultRequestHeaders.Remove("x-brand-id");
            }
            if (!string.IsNullOrWhiteSpace(brandId))
            {
                _httpClient.DefaultRequestHeaders.Add("x-brand-id", brandId);
            }
        }

        // Reporting service: Fetch POS stats
        public async Task<POS_UI.Models.PosStatsModel> GetPosStatsAsync(DateTime fromDate, DateTime toDate)
        {
            // Read required identifiers from settings.txt
            var (tenantCode, outletCode, brandIdStr) = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr))
            {
                throw new Exception("TenantCode/OutletCode/BrandId missing in settings.txt");
            }

            int brandId = 0;
            int.TryParse(brandIdStr, out brandId);

            // Bearer token from local storage (CurrentUser.ReportServiceToken)
            var localStorage = new LocalStorageService();
            var currentUser = localStorage.GetCurrentUser();
            var reportToken = currentUser?.ReportServiceToken;
            //MessageBox.Show("ReportToken: " + reportToken);
           /* if (string.IsNullOrWhiteSpace(reportToken))
            {
                // Fallback: fetch current user from API to obtain the report token
                try
                {
                    var refreshedUser = await GetCurrentUserAsync();
                    if (refreshedUser != null && !string.IsNullOrWhiteSpace(refreshedUser.ReportServiceToken))
                    {
                        reportToken = refreshedUser.ReportServiceToken;
                        // persist for subsequent calls
                        localStorage.SaveCurrentUser(refreshedUser);
                    }
                }
                catch { }

                
            }*/
            if (string.IsNullOrWhiteSpace(reportToken))
            {
                throw new Exception("ReportServiceToken not found in LocalStorage CurrentUser");
            }
            var reportBaseUrl = EnvironmentService.Instance.Config.Urls.ReportingBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(reportBaseUrl)) reportBaseUrl = "https://reporting-dev.delivergate.com";
            var endpoint = $"{reportBaseUrl}/api/v1/admin/get-dg-pos-stats";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", reportToken);

            // Build query string for GET
            var query = $"?from_date={Uri.EscapeDataString(fromDate.ToString("yyyy-MM-dd"))}" +
                        $"&to_date={Uri.EscapeDataString(toDate.ToString("yyyy-MM-dd"))}" +
                        $"&tenant_code={Uri.EscapeDataString(tenantCode)}" +
                        $"&brand_id={Uri.EscapeDataString(brandId.ToString())}" +
                        $"&outlet_code={Uri.EscapeDataString(outletCode)}";

            var response = await client.GetAsync(endpoint + query);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Reports API error {(int)response.StatusCode}: {response.StatusCode}\n{body}");
            }

            // Parse JSON into PosStatsModel (supports optional data wrapper)
            var model = new POS_UI.Models.PosStatsModel();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var payload = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Object ? dataEl : root;

            int ReadInt(string name)
            {
                if (payload.TryGetProperty(name, out var el))
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (el.TryGetInt32(out var iv)) return iv;
                        if (el.TryGetInt64(out var lv)) return (int)lv;
                    }
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(el.GetString(), out var sv))
                    {
                        return sv;
                    }
                }
                return 0;
            }

            decimal ReadDecimal(string name)
            {
                if (payload.TryGetProperty(name, out var el))
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (el.TryGetDecimal(out var dv)) return dv;
                        if (el.TryGetDouble(out var dbl)) return (decimal)dbl;
                    }
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            s = s.Replace("\u00A0", " ").Trim();
                            s = s.Replace(" ", string.Empty);
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var inv))
                            {
                                return inv;
                            }
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var cur))
                            {
                                return cur;
                            }
                            if (s.Contains(',') && !s.Contains('.'))
                            {
                                var normalized = s.Replace(',', '.');
                                if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var commaAsDot))
                                {
                                    return commaAsDot;
                                }
                            }
                        }
                    }
                }
                return 0m;
            }

            model.TotalOrders = ReadInt("total_orders");
            model.TakeawayOrders = ReadInt("takeaway_orders");
            model.DeliveryOrders = ReadInt("delivery_orders");
            model.DineInOrders = ReadInt("dine_in_orders");

            model.GrossRevenue = ReadDecimal("gross_revenue");
            model.NetRevenue = ReadDecimal("net_revenue");
            model.TakeawayRevenue = ReadDecimal("takeaway_revenue");
            model.DeliveryRevenue = ReadDecimal("delivery_revenue");
            model.DineInRevenue = ReadDecimal("dine_in_revenue");

            model.CashRevenue = ReadDecimal("cash_revenue");
            model.CardRevenue = ReadDecimal("card_revenue");

            model.CashOrders = ReadInt("cash_orders");
            model.CardOrders = ReadInt("card_orders");
            model.CancelledOrders = ReadInt("cancelled_orders");
            model.CancelledRevenue = ReadDecimal("cancelled_revenue");

            // Parse tax information
            if (payload.TryGetProperty("tax", out var taxEl) && taxEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var taxModel = new POS_UI.Models.TaxSummaryModel();
                
                // Helper function to read decimal from JsonElement
                decimal ReadDecimalFromElement(System.Text.Json.JsonElement element, string propertyName)
                {
                    if (element.TryGetProperty(propertyName, out var el))
                    {
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            if (el.TryGetDecimal(out var dv)) return dv;
                            if (el.TryGetDouble(out var dbl)) return (decimal)dbl;
                        }
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                s = s.Replace("\u00A0", " ").Trim();
                                s = s.Replace(" ", string.Empty);
                                if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var inv))
                                {
                                    return inv;
                                }
                                if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var cur))
                                {
                                    return cur;
                                }
                                if (s.Contains(',') && !s.Contains('.'))
                                {
                                    var normalized = s.Replace(',', '.');
                                    if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var commaAsDot))
                                    {
                                        return commaAsDot;
                                    }
                                }
                            }
                        }
                    }
                    return 0m;
                }

                // Helper function to read string from JsonElement
                string ReadStringFromElement(System.Text.Json.JsonElement element, string propertyName)
                {
                    if (element.TryGetProperty(propertyName, out var el))
                    {
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return el.GetString();
                        }
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            return el.ToString();
                        }
                    }
                    return string.Empty;
                }

                taxModel.TotalTaxAmount = ReadDecimalFromElement(taxEl, "total_tax_amount");
                taxModel.TotalOrderAmount = ReadDecimalFromElement(taxEl, "total_order_amount");

                if (taxEl.TryGetProperty("tax_breakdown", out var breakdownEl) && breakdownEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in breakdownEl.EnumerateObject())
                    {
                        var breakdownItem = new POS_UI.Models.TaxBreakdownItem();
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            breakdownItem.TaxRate = ReadStringFromElement(prop.Value, "tax_rate");
                            breakdownItem.TaxCode = ReadStringFromElement(prop.Value, "tax_code");
                            breakdownItem.TaxAmount = ReadDecimalFromElement(prop.Value, "tax_amount");
                            breakdownItem.OrderAmount = ReadDecimalFromElement(prop.Value, "order_amount");
                            taxModel.TaxBreakdown[prop.Name] = breakdownItem;
                        }
                    }
                }
                model.Tax = taxModel;
            }

            return model;
        }

        // Dashboard stats service: Fetch all orders stats
        public async Task<POS_UI.Models.DashboardStatsModel> GetDashboardStatsAsync(DateTime fromDate, DateTime toDate)
        {
            // Read required identifiers from settings.txt
            var (tenantCode, outletCode, brandIdStr) = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr))
            {
                throw new Exception("TenantCode/OutletCode/BrandId missing in settings.txt");
            }

            int brandId = 0;
            int.TryParse(brandIdStr, out brandId);

            // Bearer token from local storage (CurrentUser.ReportServiceToken)
            var localStorage = new LocalStorageService();
            var currentUser = localStorage.GetCurrentUser();
            var reportToken = currentUser?.ReportServiceToken;
            if (string.IsNullOrWhiteSpace(reportToken))
            {
                throw new Exception("ReportServiceToken not found in LocalStorage CurrentUser");
            }

            var reportBaseUrl = EnvironmentService.Instance.Config.Urls.ReportingBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(reportBaseUrl)) reportBaseUrl = "https://reporting-dev.delivergate.com";
            var endpoint = $"{reportBaseUrl}/api/v1/admin/dashboard-stats";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", reportToken);

            // Build request body
            var requestBody = new
            {
                from_date = fromDate.ToString("yyyy-MM-dd"),
                to_date = toDate.ToString("yyyy-MM-dd"),
                tenant_code = tenantCode,
                brand_id = brandId,
                outlet_code = new[] { outletCode }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Dashboard Stats API error {(int)response.StatusCode}: {response.StatusCode}\nURL: {endpoint}\n{body}");
            }

            // Parse JSON into DashboardStatsModel (similar to GetPosStatsAsync approach)
            var model = new POS_UI.Models.DashboardStatsModel();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var payload = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Object ? dataEl : root;

            int ReadInt(string name)
            {
                if (payload.TryGetProperty(name, out var el))
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (el.TryGetInt32(out var iv)) return iv;
                        if (el.TryGetInt64(out var lv)) return (int)lv;
                    }
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(el.GetString(), out var sv))
                    {
                        return sv;
                    }
                }
                return 0;
            }

            decimal ReadDecimal(string name)
            {
                if (payload.TryGetProperty(name, out var el))
                {
                    if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (el.TryGetDecimal(out var dv)) return dv;
                        if (el.TryGetDouble(out var dbl)) return (decimal)dbl;
                    }
                    if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                        {
                            s = s.Replace("\u00A0", " ").Trim();
                            s = s.Replace(" ", string.Empty);
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.InvariantCulture, out var inv))
                            {
                                return inv;
                            }
                            if (decimal.TryParse(s, NumberStyles.Number | NumberStyles.AllowCurrencySymbol, CultureInfo.CurrentCulture, out var cur))
                            {
                                return cur;
                            }
                            if (s.Contains(',') && !s.Contains('.'))
                            {
                                var normalized = s.Replace(',', '.');
                                if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var commaAsDot))
                                {
                                    return commaAsDot;
                                }
                            }
                        }
                    }
                }
                return 0m;
            }

            string ReadString(string name)
            {
                if (payload.TryGetProperty(name, out var el) && el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    return el.GetString() ?? string.Empty;
                }
                return string.Empty;
            }

            // Map response fields to model properties
            model.TotalOrders = ReadInt("total_orders");
            model.AcceptedOrders = ReadInt("accepted_orders");
            model.DeclinedOrders = ReadInt("declined_orders");
            model.CompletedOrders = ReadInt("completed_orders");
            model.ReadyForPickupOrders = ReadInt("ready_for_pickup_orders");
            model.CancelledOrders = ReadInt("cancelled_orders");
            model.Revenue = ReadDecimal("revenue");
            model.NetRevenue = ReadDecimal("netRevenue");

            // Handle best_platform object
            if (payload.TryGetProperty("best_platform", out var bestPlatformEl) && bestPlatformEl.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                model.BestPlatform = new POS_UI.Models.BestPlatformModel
                {
                    Name = ReadString("name"),
                    Url = ReadString("url"),
                    Count = ReadInt("count")
                };

                // Read from the best_platform object specifically
                if (bestPlatformEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    model.BestPlatform.Name = nameEl.GetString() ?? string.Empty;
                }
                if (bestPlatformEl.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    model.BestPlatform.Url = urlEl.GetString() ?? string.Empty;
                }
                if (bestPlatformEl.TryGetProperty("count", out var countEl))
                {
                    if (countEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                    {
                        if (countEl.TryGetInt32(out var count)) model.BestPlatform.Count = count;
                        else if (countEl.TryGetInt64(out var countLong)) model.BestPlatform.Count = (int)countLong;
                    }
                    else if (countEl.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(countEl.GetString(), out var count))
                    {
                        model.BestPlatform.Count = count;
                    }
                }
            }

            return model;
        }

        // Z Report: fetch platform-wise summaries from reporting service
        public async Task<System.Collections.Generic.List<POS_UI.Models.PlatformSummaryModel>> GetPlatformSummariesAsync(DateTime fromDate, DateTime toDate)
        {
            var (tenantCode, outletCode, brandIdStr) = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr))
            {
                throw new Exception("TenantCode/OutletCode/BrandId missing in settings.txt");
            }
            int brandId = 0;
            int.TryParse(brandIdStr, out brandId);

            var localStorage = new LocalStorageService();
            var currentUser = localStorage.GetCurrentUser();
            var reportToken = currentUser?.ReportServiceToken;
            if (string.IsNullOrWhiteSpace(reportToken))
            {
                throw new Exception("ReportServiceToken not found in LocalStorage CurrentUser");
            }

            var reportBaseUrl = EnvironmentService.Instance.Config.Urls.ReportingBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(reportBaseUrl)) reportBaseUrl = "https://reporting-dev.delivergate.com";
            // Platform summary endpoint for Z Report
            var endpoint = $"{reportBaseUrl}/api/v1/admin/platform-summary";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", reportToken);

            var requestBody = new
            {
                from_date = fromDate.ToString("yyyy-MM-dd"),
                to_date = toDate.ToString("yyyy-MM-dd"),
                tenant_code = tenantCode,
                brand_id = brandId,
                outlet_code = new[] { outletCode }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            System.Diagnostics.Debug.WriteLine($"[Reports] POST {endpoint} for platform summaries");
            System.Diagnostics.Debug.WriteLine($"[Reports] Request JSON: {json}");
            var response = await client.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[Reports] Response {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[Reports] Body: {body}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Dashboard Stats API error {(int)response.StatusCode}: {response.StatusCode}\nURL: {endpoint}\n{body}");
            }

            // Parse platform summaries from payload if present
            var results = new System.Collections.Generic.List<POS_UI.Models.PlatformSummaryModel>();
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var payload = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Object ? dataEl : root;

            // New shape: data.platform_revenue is an object: { "Platform Name": { "Metric": "value" ... }, ... }
            if (payload.TryGetProperty("platform_revenue", out var platRevObj) && platRevObj.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var kv in platRevObj.EnumerateObject())
                {
                    var platName = kv.Name;
                    var platData = kv.Value;
                    var model = new POS_UI.Models.PlatformSummaryModel { Name = platName };
                    if (platData.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        foreach (var metric in platData.EnumerateObject())
                        {
                            var key = metric.Name; // e.g., "Collection Orders", "Total Sales", etc.
                            var valStr = metric.Value.ValueKind == System.Text.Json.JsonValueKind.String ? (metric.Value.GetString() ?? "0") : metric.Value.ToString();
                            if (decimal.TryParse(valStr.Replace("/", string.Empty), System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowCurrencySymbol, System.Globalization.CultureInfo.InvariantCulture, out var dec))
                            {
                                model.Metrics[key] = dec;
                            }
                            else if (key.Equals("webshop_brand_name", StringComparison.OrdinalIgnoreCase))
                            {
                                model.BrandName = metric.Value.GetString();
                            }
                        }
                        // Try to synthesize order count by summing any *Orders metrics if present
                        int count = 0;
                        foreach (var m in model.Metrics)
                        {
                            if (m.Key.EndsWith("Orders", StringComparison.OrdinalIgnoreCase))
                            {
                                // Values are money, not counts, so keep count at 0; revenue printed from metrics
                            }
                        }
                        model.OrderCount = 0;
                        if (model.Metrics.TryGetValue("Total Sales", out var rev) || model.Metrics.TryGetValue("Total", out rev))
                        {
                            model.Revenue = rev;
                        }
                    }
                    results.Add(model);
                }
            }

            return results;
        }

        // Z Report Stats: Fetch platform-wise Z-report statistics from reporting service
        public async Task<POS_UI.Models.ZReportStatsModel> GetZReportStatsAsync(DateTime fromDate, DateTime toDate)
        {
            // Read required identifiers from settings.txt
            var (tenantCode, outletCode, brandIdStr) = _settingsService.LoadSettings();
            if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(outletCode) || string.IsNullOrWhiteSpace(brandIdStr))
            {
                throw new Exception("TenantCode/OutletCode/BrandId missing in settings.txt");
            }

            int brandId = 0;
            int.TryParse(brandIdStr, out brandId);

            // Bearer token from local storage (CurrentUser.ReportServiceToken)
            var localStorage = new LocalStorageService();
            var currentUser = localStorage.GetCurrentUser();
            var reportToken = currentUser?.ReportServiceToken;
            if (string.IsNullOrWhiteSpace(reportToken))
            {
                throw new Exception("ReportServiceToken not found in LocalStorage CurrentUser");
            }

            var reportBaseUrl = EnvironmentService.Instance.Config.Urls.ReportingBaseUrl?.Trim();
            if (string.IsNullOrWhiteSpace(reportBaseUrl)) reportBaseUrl = "https://reporting-dev.delivergate.com";
            var endpoint = $"{reportBaseUrl}/api/v1/admin/z-report-stats";

            // Build request body
            var requestBody = new
            {
                from = fromDate.ToString("yyyy-MM-dd HH:mm:ss"),
                to = toDate.ToString("yyyy-MM-dd HH:mm:ss"),
                tenant_code = tenantCode,
                brand_id = brandId,
                outlet_code = outletCode
            };

            var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            System.Diagnostics.Debug.WriteLine($"[ZReportStats] GET {endpoint}");
            System.Diagnostics.Debug.WriteLine($"[ZReportStats] Request JSON: {json}");
            
            // OPTIMIZATION: Use shared static HttpClient with per-request Bearer token
            // Avoids socket exhaustion and DNS resolution delays from creating new HttpClient each call
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint)
            {
                Content = content
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", reportToken);
            
            var response = await _reportingHttpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[ZReportStats] Response {(int)response.StatusCode} {response.StatusCode}");
            System.Diagnostics.Debug.WriteLine($"[ZReportStats] Body: {body}");
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Z Report Stats API error {(int)response.StatusCode}: {response.StatusCode}\nURL: {endpoint}\n{body}");
            }

            // Parse JSON response into model
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            var payload = root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == System.Text.Json.JsonValueKind.Object ? dataEl : root;

            var model = new POS_UI.Models.ZReportStatsModel();

            // Parse top-level properties
            if (payload.TryGetProperty("from", out var fromProp) && fromProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.From = fromProp.GetString();
            
            if (payload.TryGetProperty("to", out var toProp) && toProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.To = toProp.GetString();
            
            if (payload.TryGetProperty("total_net_sales", out var totalNetSalesProp) && totalNetSalesProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalNetSales = totalNetSalesProp.GetString();
            
            if (payload.TryGetProperty("total_cancellations", out var totalCancellationsProp) && totalCancellationsProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalCancellations = totalCancellationsProp.GetString();
            
            if (payload.TryGetProperty("total_missed", out var totalMissedProp) && totalMissedProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalMissed = totalMissedProp.GetString();
            
            if (payload.TryGetProperty("total_refunds", out var totalRefundsProp) && totalRefundsProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalRefunds = totalRefundsProp.GetString();
            
            if (payload.TryGetProperty("total_discount", out var totalDiscountProp) && totalDiscountProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalDiscount = totalDiscountProp.GetString();
            
            if (payload.TryGetProperty("total_gross_sales", out var totalGrossSalesProp) && totalGrossSalesProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.TotalGrossSales = totalGrossSalesProp.GetString();
            
            if (payload.TryGetProperty("restaurant_name", out var restaurantNameProp) && restaurantNameProp.ValueKind == System.Text.Json.JsonValueKind.String)
                model.RestaurantName = restaurantNameProp.GetString();

            // Parse tax_summary
            if (payload.TryGetProperty("tax_summary", out var taxSummaryProp) && taxSummaryProp.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var taxModel = new POS_UI.Models.TaxSummaryModel();
                
                // Helper function to read decimal from JsonElement (handles both string and number)
                decimal ReadDecimalFromElement(System.Text.Json.JsonElement element, string propertyName)
                {
                    if (element.TryGetProperty(propertyName, out var el))
                    {
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            if (el.TryGetDecimal(out var dv)) return dv;
                            if (el.TryGetDouble(out var dbl)) return (decimal)dbl;
                        }
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            var s = el.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                s = s.Replace("\u00A0", " ").Trim();
                                s = s.Replace(" ", string.Empty);
                                if (decimal.TryParse(s, System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowCurrencySymbol, System.Globalization.CultureInfo.InvariantCulture, out var inv))
                                {
                                    return inv;
                                }
                                if (decimal.TryParse(s, System.Globalization.NumberStyles.Number | System.Globalization.NumberStyles.AllowCurrencySymbol, System.Globalization.CultureInfo.CurrentCulture, out var cur))
                                {
                                    return cur;
                                }
                                if (s.Contains(',') && !s.Contains('.'))
                                {
                                    var normalized = s.Replace(',', '.');
                                    if (decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var commaAsDot))
                                    {
                                        return commaAsDot;
                                    }
                                }
                            }
                        }
                    }
                    return 0m;
                }

                // Helper function to read string from JsonElement
                string ReadStringFromElement(System.Text.Json.JsonElement element, string propertyName)
                {
                    if (element.TryGetProperty(propertyName, out var el))
                    {
                        if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return el.GetString();
                        }
                        if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            return el.ToString();
                        }
                    }
                    return string.Empty;
                }

                // Parse total_tax_amount and total_order_amount
                taxModel.TotalTaxAmount = ReadDecimalFromElement(taxSummaryProp, "total_tax_amount");
                taxModel.TotalOrderAmount = ReadDecimalFromElement(taxSummaryProp, "total_order_amount");

                // Parse tax_breakdown
                if (taxSummaryProp.TryGetProperty("tax_breakdown", out var breakdownEl) && breakdownEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var prop in breakdownEl.EnumerateObject())
                    {
                        var taxRateKey = prop.Name; // The key is the tax rate (e.g., "0.00", "15.00", "20.00")
                        var breakdownItem = new POS_UI.Models.TaxBreakdownItem();
                        
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                        {
                            breakdownItem.TaxRate = ReadStringFromElement(prop.Value, "tax_rate");
                            breakdownItem.TaxCode = ReadStringFromElement(prop.Value, "tax_code");
                            breakdownItem.TaxAmount = ReadDecimalFromElement(prop.Value, "tax_amount");
                            breakdownItem.OrderAmount = ReadDecimalFromElement(prop.Value, "order_amount");
                            
                            taxModel.TaxBreakdown[taxRateKey] = breakdownItem;
                        }
                    }
                }
                
                model.TaxSummary = taxModel;
            }

            // Parse platform_stats
            if (payload.TryGetProperty("platform_stats", out var platformStatsProp) && platformStatsProp.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var platformKv in platformStatsProp.EnumerateObject())
                {
                    var platformName = platformKv.Name;
                    var platformData = platformKv.Value;
                    
                    var platformModel = new POS_UI.Models.PlatformStatsModel();
                    
                    // Parse platform_id
                    if (platformData.TryGetProperty("platform_id", out var platformIdProp))
                    {
                        if (platformIdProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                            platformModel.PlatformId = platformIdProp.GetInt32();
                        else if (platformIdProp.ValueKind == System.Text.Json.JsonValueKind.String && int.TryParse(platformIdProp.GetString(), out var pid))
                            platformModel.PlatformId = pid;
                    }

                    // Parse gross_sales
                    if (platformData.TryGetProperty("gross_sales", out var grossSalesProp) && grossSalesProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var grossSales = new POS_UI.Models.GrossSalesModel();
                        
                        // Collection/Delivery (Webshop, Table Order)
                        if (grossSalesProp.TryGetProperty("collection_orders", out var colOrdersProp))
                            grossSales.CollectionOrders = colOrdersProp.ValueKind == System.Text.Json.JsonValueKind.Number ? colOrdersProp.GetInt32() : (int.TryParse(colOrdersProp.GetString(), out var co) ? co : (int?)null);
                        if (grossSalesProp.TryGetProperty("collection_revenue", out var colRevProp) && colRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            grossSales.CollectionRevenue = colRevProp.GetString();
                        if (grossSalesProp.TryGetProperty("delivery_orders", out var delOrdersProp))
                            grossSales.DeliveryOrders = delOrdersProp.ValueKind == System.Text.Json.JsonValueKind.Number ? delOrdersProp.GetInt32() : (int.TryParse(delOrdersProp.GetString(), out var do_) ? do_ : (int?)null);
                        if (grossSalesProp.TryGetProperty("delivery_order_revenue", out var delRevProp) && delRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            grossSales.DeliveryOrderRevenue = delRevProp.GetString();
                        
                        // Takeaway/DineIn (DG POS)
                        if (grossSalesProp.TryGetProperty("takeaway_orders", out var takeawayOrdersProp))
                            grossSales.TakeawayOrders = takeawayOrdersProp.ValueKind == System.Text.Json.JsonValueKind.Number ? takeawayOrdersProp.GetInt32() : (int.TryParse(takeawayOrdersProp.GetString(), out var to) ? to : (int?)null);
                        if (grossSalesProp.TryGetProperty("takeaway_order_revenue", out var takeawayRevProp) && takeawayRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            grossSales.TakeawayOrderRevenue = takeawayRevProp.GetString();
                        if (grossSalesProp.TryGetProperty("dine_in_orders", out var dineInOrdersProp))
                            grossSales.DineInOrders = dineInOrdersProp.ValueKind == System.Text.Json.JsonValueKind.Number ? dineInOrdersProp.GetInt32() : (int.TryParse(dineInOrdersProp.GetString(), out var dio) ? dio : (int?)null);
                        if (grossSalesProp.TryGetProperty("dine_in_order_revenue", out var dineInRevProp) && dineInRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            grossSales.DineInOrderRevenue = dineInRevProp.GetString();
                        
                        platformModel.GrossSales = grossSales;
                    }

                    // Parse tender_summary
                    if (platformData.TryGetProperty("tender_summary", out var tenderSummaryProp) && tenderSummaryProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var tenderSummary = new POS_UI.Models.TenderSummaryModel();
                        
                        if (tenderSummaryProp.TryGetProperty("cash_order_count", out var cashOrderCountProp))
                            tenderSummary.CashOrderCount = cashOrderCountProp.ValueKind == System.Text.Json.JsonValueKind.Number ? cashOrderCountProp.GetInt32() : (int.TryParse(cashOrderCountProp.GetString(), out var coc) ? coc : 0);
                        if (tenderSummaryProp.TryGetProperty("cash_revenue", out var cashRevProp) && cashRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            tenderSummary.CashRevenue = cashRevProp.GetString();
                        
                        // Card Online/Machine (Webshop, Table Order)
                        if (tenderSummaryProp.TryGetProperty("card_online_order_count", out var cardOnlineCountProp))
                            tenderSummary.CardOnlineOrderCount = cardOnlineCountProp.ValueKind == System.Text.Json.JsonValueKind.Number ? cardOnlineCountProp.GetInt32() : (int.TryParse(cardOnlineCountProp.GetString(), out var coc2) ? coc2 : (int?)null);
                        if (tenderSummaryProp.TryGetProperty("card_online_revenue", out var cardOnlineRevProp) && cardOnlineRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            tenderSummary.CardOnlineRevenue = cardOnlineRevProp.GetString();
                        if (tenderSummaryProp.TryGetProperty("card_machine_order_count", out var cardMachineCountProp))
                            tenderSummary.CardMachineOrderCount = cardMachineCountProp.ValueKind == System.Text.Json.JsonValueKind.Number ? cardMachineCountProp.GetInt32() : (int.TryParse(cardMachineCountProp.GetString(), out var cmc) ? cmc : (int?)null);
                        if (tenderSummaryProp.TryGetProperty("card_machine_revenue", out var cardMachineRevProp) && cardMachineRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            tenderSummary.CardMachineRevenue = cardMachineRevProp.GetString();
                        
                        // Card (DG POS)
                        if (tenderSummaryProp.TryGetProperty("card_order_count", out var cardOrderCountProp))
                            tenderSummary.CardOrderCount = cardOrderCountProp.ValueKind == System.Text.Json.JsonValueKind.Number ? cardOrderCountProp.GetInt32() : (int.TryParse(cardOrderCountProp.GetString(), out var coc3) ? coc3 : (int?)null);
                        if (tenderSummaryProp.TryGetProperty("card_revenue", out var cardRevProp) && cardRevProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            tenderSummary.CardRevenue = cardRevProp.GetString();
                        
                        platformModel.TenderSummary = tenderSummary;
                    }

                    // Parse refund_summary
                    if (platformData.TryGetProperty("refund_summary", out var refundSummaryProp) && refundSummaryProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var refundSummary = new POS_UI.Models.RefundSummaryModel();
                        if (refundSummaryProp.TryGetProperty("cash_refund", out var cashRefundProp) && cashRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.CashRefund = cashRefundProp.GetString();
                        if (refundSummaryProp.TryGetProperty("card_refund", out var cardRefundProp) && cardRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.CardRefund = cardRefundProp.GetString();
                        if (refundSummaryProp.TryGetProperty("total_refund", out var totalRefundProp) && totalRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.TotalRefund = totalRefundProp.GetString();
                        if (refundSummaryProp.TryGetProperty("card_online_refund", out var cardOnlineRefundProp) && cardOnlineRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.CardOnlineRefund = cardOnlineRefundProp.GetString();
                        if (refundSummaryProp.TryGetProperty("cash_sale_cash_refund", out var cashSaleCashRefundProp) && cashSaleCashRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.CashSaleCashRefund = cashSaleCashRefundProp.GetString();
                        if (refundSummaryProp.TryGetProperty("card_sale_cash_refund", out var cardSaleCashRefundProp) && cardSaleCashRefundProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            refundSummary.CardSaleCashRefund = cardSaleCashRefundProp.GetString();
                        platformModel.RefundSummary = refundSummary;
                    }

                    // Parse unfulfilled_summary
                    if (platformData.TryGetProperty("unfulfilled_summary", out var unfulfilledSummaryProp) && unfulfilledSummaryProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var unfulfilledSummary = new POS_UI.Models.UnfulfilledSummaryModel();
                        if (unfulfilledSummaryProp.TryGetProperty("cancellations", out var cancellationsProp) && cancellationsProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            unfulfilledSummary.Cancellations = cancellationsProp.GetString();
                        if (unfulfilledSummaryProp.TryGetProperty("missed", out var missedProp) && missedProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            unfulfilledSummary.Missed = missedProp.GetString();
                        if (unfulfilledSummaryProp.TryGetProperty("voids", out var voidsProp) && voidsProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            unfulfilledSummary.Voids = voidsProp.GetString();
                        platformModel.UnfulfilledSummary = unfulfilledSummary;
                    }

                    // Parse discount_summary
                    if (platformData.TryGetProperty("discount_summary", out var discountSummaryProp) && discountSummaryProp.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var discountSummary = new POS_UI.Models.DiscountSummaryModel();
                        if (discountSummaryProp.TryGetProperty("discount", out var discountProp) && discountProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            discountSummary.Discount = discountProp.GetString();
                        if (discountSummaryProp.TryGetProperty("discount_order_count", out var discountOrderCountProp))
                            discountSummary.DiscountOrderCount = discountOrderCountProp.ValueKind == System.Text.Json.JsonValueKind.Number ? discountOrderCountProp.GetInt32() : (int.TryParse(discountOrderCountProp.GetString(), out var parsedDiscountCount) ? parsedDiscountCount : 0);
                        if (discountSummaryProp.TryGetProperty("voucher_discount", out var voucherDiscountProp) && voucherDiscountProp.ValueKind == System.Text.Json.JsonValueKind.String)
                            discountSummary.VoucherDiscount = voucherDiscountProp.GetString();
                        platformModel.DiscountSummary = discountSummary;
                    }

                    model.PlatformStats[platformName] = platformModel;
                }

                // Calculate all order counts using the model's method
                model.CalculateOrderCounts();
            }

            return model;
        }

        public async Task<(string accessToken, string refreshToken, DateTime accessTokenExpiry, DateTime refreshTokenExpiry)> LoginAsync(string email, string pin)
        {
            try
            {
                // Load settings first
                var (_, settingsOutletCode, settingsBrandId) = _settingsService.LoadSettings();
                var outletCode = settingsOutletCode ?? string.Empty;
                var brandId = settingsBrandId ?? string.Empty;
                
                var form = new[]
                {
                    new KeyValuePair<string, string>("email", email),
                    new KeyValuePair<string, string>("pin", pin),
                    new KeyValuePair<string, string>("OutletCode", outletCode),
                    new KeyValuePair<string, string>("BrandID", brandId)

                };
                var content = new FormUrlEncodedContent(form);

                var response = await _httpClient.PostAsync("/api/v1/auth/login", content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                string accessToken = data.GetProperty("accessToken").GetString();
                string refreshToken = data.GetProperty("refreshToken").GetString();
                DateTime accessTokenExpiry = DateTime.MinValue;
                DateTime refreshTokenExpiry = DateTime.MinValue;
                if (data.TryGetProperty("accessTokenExpiry", out var accessExpProp))
                {
                    if (accessExpProp.ValueKind == JsonValueKind.Number)
                        accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(accessExpProp.GetInt64()).UtcDateTime;
                    else if (accessExpProp.ValueKind == JsonValueKind.String)
                        accessTokenExpiry = DateTime.Parse(accessExpProp.GetString()).ToUniversalTime();
                }
                if (data.TryGetProperty("refreshTokenExpiry", out var refreshExpProp))
                {
                    if (refreshExpProp.ValueKind == JsonValueKind.Number)
                        refreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(refreshExpProp.GetInt64()).UtcDateTime;
                    else if (refreshExpProp.ValueKind == JsonValueKind.String)
                        refreshTokenExpiry = DateTime.Parse(refreshExpProp.GetString()).ToUniversalTime();
                }
                
                return (accessToken, refreshToken, accessTokenExpiry, refreshTokenExpiry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login Error: {ex.Message}");
                throw;
            }
        }

        public async Task<(string accessToken, string refreshToken, DateTime accessTokenExpiry, DateTime refreshTokenExpiry)> RefreshTokenAsync(string refreshToken)
        {
            try
            {  
                var form = new[]
                {
                    new KeyValuePair<string, string>("refreshToken", refreshToken)
                };
                var content = new FormUrlEncodedContent(form);
                

                //System.Windows.MessageBox.Show("Making API call to /api/v1/auth/refresh...");
                //Console.WriteLine("Making API call to /api/v1/auth/refresh...");
                //Console.WriteLine($"Full URL: {_httpClient.BaseAddress}/api/v1/auth/refresh");
                HttpResponseMessage response;
                try
                {
                    // Add a timeout to prevent hanging
                    using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                    //Console.WriteLine("Starting HTTP request with 5-second timeout...");
                    //Console.WriteLine("About to call PostAsync...");
                    
                    // Test if we can reach the base URL first
                    try
                    {
                        //Console.WriteLine("Testing base URL connectivity...");
                       // var testResponse = await _httpClient.GetAsync("", cts.Token);
                        //Console.WriteLine($"Base URL test response: {testResponse.StatusCode}");
                    }
                    catch (Exception testEx)
                    {
                        //Console.WriteLine($"Base URL test failed: {testEx.Message}");
                        // Continue anyway, the main request might still work
                    }
                    
                    response = await _httpClient.PostAsync("/api/v1/auth/refresh", content, cts.Token);
                    //Console.WriteLine($"API response status: {response.StatusCode}");
                    //System.Windows.MessageBox.Show($"API response status: {response.StatusCode}");
                }
                catch (System.Threading.Tasks.TaskCanceledException)
                {
                    //Console.WriteLine("API call timed out after 5 seconds");
                    //System.Windows.MessageBox.Show("API call timed out after 5 seconds");
                    throw new Exception("API call timed out");
                }
                catch (System.Net.Http.HttpRequestException httpEx)
                {
                    //Console.WriteLine($"HTTP request failed: {httpEx.Message}");
                    //System.Windows.MessageBox.Show($"HTTP request failed: {httpEx.Message}");
                    throw;
                }
                catch (Exception ex)
                {
                    //Console.WriteLine($"Unexpected error during API call: {ex.Message}");
                    //Console.WriteLine($"Stack trace: {ex.StackTrace}");
                    //System.Windows.MessageBox.Show($"Unexpected error during API call: {ex.Message}");
                    throw;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    //Console.WriteLine($"API error: {error}");
                    //System.Windows.MessageBox.Show($"API error: {error}");
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on refresh"); } catch { }
                    }
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                //System.Windows.MessageBox.Show("Reading response content...");
                //Console.WriteLine("Reading response content...");
                var json = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"Response JSON: {json}");
                //System.Windows.MessageBox.Show($"Response JSON: {json}");
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                //System.Windows.MessageBox.Show("Parsing token data...");
                Console.WriteLine("Parsing token data...");
                string accessToken = data.GetProperty("accessToken").GetString();
                string newRefreshToken = data.GetProperty("refreshToken").GetString();
                DateTime accessTokenExpiry = DateTime.MinValue;
                DateTime refreshTokenExpiry = DateTime.MinValue;
                if (data.TryGetProperty("accessTokenExpiry", out var accessExpProp))
                {
                    if (accessExpProp.ValueKind == JsonValueKind.Number)
                        accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(accessExpProp.GetInt64()).UtcDateTime;
                    else if (accessExpProp.ValueKind == JsonValueKind.String)
                        accessTokenExpiry = DateTime.Parse(accessExpProp.GetString()).ToUniversalTime();
                }
                if (data.TryGetProperty("refreshTokenExpiry", out var refreshExpProp))
                {
                    if (refreshExpProp.ValueKind == JsonValueKind.Number)
                        refreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(refreshExpProp.GetInt64()).UtcDateTime;
                    else if (refreshExpProp.ValueKind == JsonValueKind.String)
                        refreshTokenExpiry = DateTime.Parse(refreshExpProp.GetString()).ToUniversalTime();
                }
                //System.Windows.MessageBox.Show("RefreshTokenAsync completed successfully");
                Console.WriteLine("RefreshTokenAsync completed successfully");
                return (accessToken, newRefreshToken, accessTokenExpiry, refreshTokenExpiry);
            }
            catch (Exception ex)
            {
                Console.WriteLine("RefreshTokenAsync error: " + ex.Message);
                Console.WriteLine("Stack trace: " + ex.StackTrace);
                //System.Windows.MessageBox.Show("RefreshTokenAsync error: " + ex.Message + "\n\nStackTrace: " + ex.StackTrace);
                throw; // Re-throw to be caught by the calling method
            }
        }

        public void SetBearerToken(string accessToken)
        {
            // Remove existing Authorization header if it exists
            _httpClient.DefaultRequestHeaders.Remove("Authorization");
            
            // Set the Authorization header with the provided access token
            _httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }
        public async Task<bool> LogoutAsync()
        {
            try
            { 
                //Set bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var response = await _httpClient.DeleteAsync("/api/v1/auth/logout");

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Logout API error: Status: {response.StatusCode}\n{error}");
                    return false;
                }

                Console.WriteLine("Logout API call successful");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Logout API error: {ex.Message}");
                return false;
            }
        }
        public async Task<string> PlaceOrderAsync(object orderRequest)
        {
            var json = JsonSerializer.Serialize(orderRequest);
            try
            {
                var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pos-create-{DateTime.Now:yyyyMMdd_HHmmssfff}.json");
                System.IO.File.WriteAllText(path, json);
                Debug.WriteLine($"[CreateOrder] Payload written to {path}\n{json}");
            }
            catch { /* ignore diagnostics failure */ }
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/v1/orders", content);
            var responseBody = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Status: {response.StatusCode}\n{responseBody}");
            }
            return responseBody;
        }

        public async Task<bool> UpdateOrderAsync(int orderId, object orderRequest)
        {
            try
            {
                // Check if user is logged in
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to update order.");
                }
                
                SetBearerToken(accessToken);

                // Ensure tenant and outlet headers are present
                var (tenantCode, outletCode, _) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    if (_httpClient.DefaultRequestHeaders.Contains("x-tenant-code"))
                    {
                        _httpClient.DefaultRequestHeaders.Remove("x-tenant-code");
                    }
                    _httpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }
                if (!string.IsNullOrWhiteSpace(outletCode))
                {
                    if (_httpClient.DefaultRequestHeaders.Contains("x-outlet-code"))
                    {
                        _httpClient.DefaultRequestHeaders.Remove("x-outlet-code");
                    }
                    _httpClient.DefaultRequestHeaders.Add("x-outlet-code", outletCode);
                }

                var json = JsonSerializer.Serialize(orderRequest);
                try
                {
                    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pos-update-{orderId}-{DateTime.Now:yyyyMMdd_HHmmssfff}.json");
                    System.IO.File.WriteAllText(path, json);
                    Debug.WriteLine($"[UpdateOrder] Endpoint: /api/v1/orders/{orderId}\nPayload written to {path}\n{json}");
                }
                catch { /* ignore diagnostics failure */ }

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var endpoint = $"/api/v1/orders/{orderId}";

                var response = await _httpClient.PutAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\nEndpoint: {endpoint}\nBody: {json}\nResponse: {responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\nEndpoint: {endpoint}\nBody: {json}\nResponse: {responseBody}");
                }

                return true;
            }
            catch (TaskCanceledException)
            {
                // Suppress timeout UI for kiosk stability
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating order: {ex.Message}");
                throw;
            }
        }
       /*public void UpdateTenantCode()
        {
            var (tenantCode, outletCode) = _settingsService.LoadSettings();
            _httpClient.DefaultRequestHeaders.Remove("x-tenant-code");
            _httpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
            _httpClient.DefaultRequestHeaders.Remove("x-outlet-code");
            _httpClient.DefaultRequestHeaders.Add("x-outlet-code", outletCode);
        }*/

        public async Task<List<POS_UI.Models.UserModel>> GetUsersAsync(string outletCode = null, int? roles = null)
        {
            // Set bearer token from settings
            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
            {
                SetBearerToken(accessToken);
            }

            // If no outlet code provided, try to get it from settings
            if (string.IsNullOrEmpty(outletCode))
            {
                var (_, settingsOutletCode, _) = _settingsService.LoadSettings();
                outletCode = settingsOutletCode;
            }

            // Build the API URL with optional parameters
            var apiUrl = "/api/v1/users";
            var queryParams = new List<string>();
            
            if (!string.IsNullOrEmpty(outletCode))
            {
                queryParams.Add($"outlet-code={Uri.EscapeDataString(outletCode)}");
            }
            
            if (roles.HasValue)
            {
                queryParams.Add($"roles={roles.Value}");
            }
            
            if (queryParams.Count > 0)
            {
                apiUrl += "?" + string.Join("&", queryParams);
            }

            // Log request context (without secrets)
            try { POS_UI.Services.LogService.Info($"GET {apiUrl} (outlet={outletCode ?? "<none>"}, roles={roles?.ToString() ?? "<none>"})"); } catch { }

            var startedAt = DateTime.UtcNow;
            var response = await _httpClient.GetAsync(apiUrl);
            var elapsedMs = (int)(DateTime.UtcNow - startedAt).TotalMilliseconds;
            try { POS_UI.Services.LogService.Info($"Response {((int)response.StatusCode)} from {apiUrl} in {elapsedMs}ms"); } catch { }
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                try { POS_UI.Services.LogService.Error($"GetUsers failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {Truncate(error, 2000)}"); } catch { }
                throw new Exception($"Status: {response.StatusCode}\n{error}");
            }

            var json = await response.Content.ReadAsStringAsync();
            try { POS_UI.Services.LogService.Info($"GetUsers OK: {json?.Length ?? 0} bytes"); } catch { }
            //System.Windows.MessageBox.Show("User API JSON:\n" + json);
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");
            var users = new List<POS_UI.Models.UserModel>();
            foreach (var userElem in data.EnumerateArray())
            {
                users.Add(new POS_UI.Models.UserModel
                {
                    ApiId = userElem.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                    FirstName = userElem.GetProperty("first_name").GetString(),
                    LastName = userElem.GetProperty("last_name").GetString(),
                    Email = userElem.GetProperty("email").GetString(),
                    Role = userElem.GetProperty("role").GetString(),
                });
            }
            return users;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        /*public async Task<POS_UI.Models.UserModel> GetUserByEmailAsync(string email)
        {
            var users = await GetUsersAsync();
            return users.FirstOrDefault(u => u.Email != null && u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }*/

        public async Task<POS_UI.Models.UserModel> GetUserByIdAsync(string id)
        {
            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
            {
                SetBearerToken(accessToken);
            }
            var response = await _httpClient.GetAsync($"/api/v1/users/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception($"Status: {response.StatusCode}\n{error}");
            }
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var data = doc.RootElement.GetProperty("data");

            string GetString(JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                    return element.GetString();
                if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("String", out var strProp))
                    return strProp.GetString();
                return "";
            }

            return new POS_UI.Models.UserModel
            {
                ApiId = data.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0,
                FirstName = data.TryGetProperty("first_name", out var nameProp) ? GetString(nameProp) : "",
                LastName = data.TryGetProperty("last_name", out var lastNameProp) ? GetString(lastNameProp) : "",
                Email = data.TryGetProperty("email", out var emailProp) ? GetString(emailProp) : "",
                Role = data.TryGetProperty("role", out var roleProp) ? GetString(roleProp) : "",
                Phone = data.TryGetProperty("contact_no", out var phoneProp) ? GetString(phoneProp) : "",
                Address = data.TryGetProperty("address", out var addressProp) ? GetString(addressProp) : "",
                RoleId = data.TryGetProperty("role_id", out var roleIdProp) ? GetString(roleIdProp) : ""
            
            };
        }

        public async Task<(List<string> Categories, List<Models.ProductItemModel> Products)> GetProductsAndCategoriesAsync()
        {
            try
            {
                //Retrieves the stored access token from application settings.
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                // Get the selected menu from shop details
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                var selectedMenu = shopDetails?.DeliveryPlatform.SelectedMenu ?? 65; // Fallback to 65 if shop details not available

                // Get brand ID from settings and shop ID from shop details
                var (_, _, brandId) = _settingsService.LoadSettings();
                var shopId = shopDetails?.Id ?? 2; // Fallback to 2 if shop details not available

                //Makes an HTTP GET request to the API endpoint to retrieve the products and categories.
                var response = await _httpClient.GetAsync($"/api/v1/main-menu/{selectedMenu}/categories/webshop-brand/{brandId}/shop/{shopId}");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }
                //Reads the response content as a string.
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                //Initializes two lists to store the categories and products.
                var categories = new List<string>();
                var products = new List<Models.ProductItemModel>();

                //Iterates through each category in the data.
                foreach (var categoryProperty in data.EnumerateObject())
                {
                    var categoryName = categoryProperty.Name;
                    categories.Add(categoryName);

                    if (categoryProperty.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var productElement in categoryProperty.Value.EnumerateArray())
                        {
                            var product = new Models.ProductItemModel
                            {
                                Id = productElement.TryGetProperty("id", out var idElement) ? idElement.GetInt32() : 0,
                                ItemName = productElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : "No Name",
                                Category = categoryName,
                                CategoryId = productElement.TryGetProperty("item_category_id", out var categoryIdElement) ? categoryIdElement.GetInt32() : 0,
                                //ImageUrl = productElement.TryGetProperty("image_url", out var imageElement) && imageElement.ValueKind == JsonValueKind.String ? imageElement.GetString() : null
                            };

                            if (productElement.TryGetProperty("tax_profile_id", out var productTaxProfileElement) && productTaxProfileElement.ValueKind == JsonValueKind.Number)
                            {
                                product.TaxProfileId = productTaxProfileElement.GetInt32();
                            }
                            else if (productElement.TryGetProperty("tax_profile", out var productTaxProfileObj) && productTaxProfileObj.ValueKind == JsonValueKind.Object)
                            {
                                if (productTaxProfileObj.TryGetProperty("id", out var productTaxProfileIdProp) && productTaxProfileIdProp.ValueKind == JsonValueKind.Number)
                                {
                                    product.TaxProfileId = productTaxProfileIdProp.GetInt32();
                                }
                            }

                            if (productElement.TryGetProperty("price", out var priceElement) && decimal.TryParse(priceElement.GetString(), out decimal price))
                            {
                                product.Price = price;
                            }
                            else
                            {
                                product.Price = 0.0m;
                            }

                            // Parse modifiers if they exist
                            if (productElement.TryGetProperty("modifiers", out var modifiersElement) && modifiersElement.ValueKind == JsonValueKind.Array)
                            {
                                var modifiers = new List<Models.ModifierModel>();
                                foreach (var modifierElement in modifiersElement.EnumerateArray())
                                {
                                    // Get the modifier object
                                    if (modifierElement.TryGetProperty("modifier", out var modifierObj))
                                    {
                                        var modifier = new Models.ModifierModel
                                        {
                                            Id = modifierObj.TryGetProperty("id", out var modIdElement) ? modIdElement.GetInt32() : 0,
                                            Title = modifierObj.TryGetProperty("title", out var modTitleElement) ? modTitleElement.GetString() : "#####",
                                            MinPermitted = modifierObj.TryGetProperty("min_permitted", out var minPermittedElement) ? minPermittedElement.GetInt32() : 0,
                                            MaxPermitted = modifierObj.TryGetProperty("max_permitted", out var maxPermittedElement) ? maxPermittedElement.GetInt32() : 0,
                                            DefaultQuantity = modifierObj.TryGetProperty("default_quantity", out var defaultQuantityElement) ? defaultQuantityElement.GetInt32() : 0
                                        };
                                        modifier.IsTaxInherited =
                                            TryGetBoolean(modifierObj, "is_inherited") ||
                                            TryGetBoolean(modifierElement, "is_inherited");
                                        //MessageBox.Show($"Modifier: {modifier.Title}, MinPermitted: {modifier.MinPermitted}, MaxPermitted: {modifier.MaxPermitted}");

                                        // Parse modifier items if they exist
                                        if (modifierElement.TryGetProperty("items", out var itemsElement) && itemsElement.ValueKind == JsonValueKind.Array)
                                        {
                                            var modifierItems = new List<Models.ModifierItemModel>();
                                            foreach (var itemElement in itemsElement.EnumerateArray())
                                            {
                                                decimal priceValue = 0.0m;
                                                if (itemElement.TryGetProperty("price", out var itemPriceElement))
                                                {
                                                    if (itemPriceElement.ValueKind == JsonValueKind.Number)
                                                        priceValue = itemPriceElement.GetDecimal();
                                                    else if (itemPriceElement.ValueKind == JsonValueKind.String && decimal.TryParse(itemPriceElement.GetString(), out decimal parsed))
                                                        priceValue = parsed;
                                                }
                                                var modifierItem = new Models.ModifierItemModel
                                                {
                                                    Id = itemElement.TryGetProperty("id", out var itemIdElement) ? itemIdElement.GetInt32() : 0,
                                                    ItemName = itemElement.TryGetProperty("item_name", out var itemNameElement) ? itemNameElement.GetString() : "#####",
                                                    ItemPrice = priceValue,
                                                    ExternalItemId = itemElement.TryGetProperty("external_item_id", out var entityIdElement) ? entityIdElement.GetString() : null
                                                };

                                                if (itemElement.TryGetProperty("tax_profile_id", out var modifierTaxProfileElement) && modifierTaxProfileElement.ValueKind == JsonValueKind.Number)
                                                {
                                                    modifierItem.TaxProfileId = modifierTaxProfileElement.GetInt32();
                                                }
                                                else if (itemElement.TryGetProperty("tax_profile", out var modifierTaxProfileObj) && modifierTaxProfileObj.ValueKind == JsonValueKind.Object)
                                                {
                                                    if (modifierTaxProfileObj.TryGetProperty("id", out var modifierTaxProfileIdProp) && modifierTaxProfileIdProp.ValueKind == JsonValueKind.Number)
                                                    {
                                                        modifierItem.TaxProfileId = modifierTaxProfileIdProp.GetInt32();
                                                    }
                                                }

                                                // Parse nested modifiers if they exist
                                                if (itemElement.TryGetProperty("modifier_list", out var nestedModifiersElement) && nestedModifiersElement.ValueKind == JsonValueKind.Array)
                                                {
                                                    var nestedModifiers = new List<Models.ModifierModel>();
                                                    foreach (var nestedModifierElement in nestedModifiersElement.EnumerateArray())
                                                    {
                                                        if (nestedModifierElement.TryGetProperty("modifier", out var nestedModifierObj))
                                                        {
                                                            var nestedModifier = new Models.ModifierModel
                                                            {
                                                                Id = nestedModifierObj.TryGetProperty("id", out var nestedModIdElement) ? nestedModIdElement.GetInt32() : 0,
                                                                Title = nestedModifierObj.TryGetProperty("title", out var nestedModTitleElement) ? nestedModTitleElement.GetString() : "#####",
                                                                MinPermitted = nestedModifierObj.TryGetProperty("min_permitted", out var nestedMinPermittedElement) ? nestedMinPermittedElement.GetInt32() : 0,
                                                                MaxPermitted = nestedModifierObj.TryGetProperty("max_permitted", out var nestedMaxPermittedElement) ? nestedMaxPermittedElement.GetInt32() : 0,
                                                                DefaultQuantity = nestedModifierObj.TryGetProperty("default_quantity", out var nestedDefaultQuantityElement) ? nestedDefaultQuantityElement.GetInt32() : 0
                                                            };
                                                            nestedModifier.IsTaxInherited =
                                                                TryGetBoolean(nestedModifierObj, "is_inherited") ||
                                                                TryGetBoolean(nestedModifierElement, "is_inherited");

                                                            // Parse nested modifier items if they exist
                                                            if (nestedModifierElement.TryGetProperty("items", out var nestedItemsElement) && nestedItemsElement.ValueKind == JsonValueKind.Array)
                                                            {
                                                                var nestedModifierItems = new List<Models.ModifierItemModel>();
                                                                foreach (var nestedItemElement in nestedItemsElement.EnumerateArray())
                                                                {
                                                                    decimal nestedPriceValue = 0.0m;
                                                                    if (nestedItemElement.TryGetProperty("price", out var nestedItemPriceElement))
                                                                    {
                                                                        if (nestedItemPriceElement.ValueKind == JsonValueKind.Number)
                                                                            nestedPriceValue = nestedItemPriceElement.GetDecimal();
                                                                        else if (nestedItemPriceElement.ValueKind == JsonValueKind.String && decimal.TryParse(nestedItemPriceElement.GetString(), out decimal parsed))
                                                                            nestedPriceValue = parsed;
                                                                    }
                                                                    var nestedModifierItem = new Models.ModifierItemModel
                                                                    {
                                                                        Id = nestedItemElement.TryGetProperty("id", out var nestedItemIdElement) ? nestedItemIdElement.GetInt32() : 0,
                                                                        ItemName = nestedItemElement.TryGetProperty("item_name", out var nestedItemNameElement) ? nestedItemNameElement.GetString() : "#####",
                                                                        ItemPrice = nestedPriceValue,
                                                                        ExternalItemId = nestedItemElement.TryGetProperty("external_item_id", out var nestedEntityIdElement) ? nestedEntityIdElement.GetString() : null
                                                                    };
                                                                    if (nestedItemElement.TryGetProperty("tax_profile_id", out var nestedTaxProfileElement) && nestedTaxProfileElement.ValueKind == JsonValueKind.Number)
                                                                    {
                                                                        nestedModifierItem.TaxProfileId = nestedTaxProfileElement.GetInt32();
                                                                    }
                                                                    else if (nestedItemElement.TryGetProperty("tax_profile", out var nestedTaxProfileObj) && nestedTaxProfileObj.ValueKind == JsonValueKind.Object)
                                                                    {
                                                                        if (nestedTaxProfileObj.TryGetProperty("id", out var nestedTaxProfileIdProp) && nestedTaxProfileIdProp.ValueKind == JsonValueKind.Number)
                                                                        {
                                                                            nestedModifierItem.TaxProfileId = nestedTaxProfileIdProp.GetInt32();
                                                                        }
                                                                    }
                                                                    nestedModifierItems.Add(nestedModifierItem);
                                                                }
                                                                nestedModifier.ModifierItems = nestedModifierItems;
                                                            }
                                                            else
                                                            {
                                                                // If no nested items, create a default item with "#####"
                                                                nestedModifier.ModifierItems = new List<Models.ModifierItemModel>
                                                                {
                                                                    new Models.ModifierItemModel
                                                                    {
                                                                        Id = 0,
                                                                        ItemName = "#####",
                                                                        ItemPrice = 0.0m
                                                                    }
                                                                };
                                                            }
                                                            nestedModifiers.Add(nestedModifier);
                                                        }
                                                    }
                                                    modifierItem.NestedModifiers = nestedModifiers;
                                                }
                                                modifierItems.Add(modifierItem);
                                            }
                                            modifier.ModifierItems = modifierItems;
                                        }
                                        else
                                        {
                                            // If no items, create a default item with "#####"
                                            modifier.ModifierItems = new List<Models.ModifierItemModel>
                                            {
                                                new Models.ModifierItemModel
                                                {
                                                    Id = 0,
                                                    ItemName = "#####",
                                                    ItemPrice = 0.0m
                                                }
                                            };
                                        }
                                        modifiers.Add(modifier);
                                    }
                                }
                                product.Modifiers = modifiers;
                            }
                            else
                            {
                                // If no modifiers, create a default modifier with "#####"
                                product.Modifiers = new List<Models.ModifierModel>
                                {
                                    new Models.ModifierModel
                                    {
                                        Id = 0,
                                        Title = "#####",
                                        ModifierItems = new List<Models.ModifierItemModel>
                                        {
                                            new Models.ModifierItemModel
                                            {
                                                Id = 0,
                                                ItemName = "#####",
                                                ItemPrice = 0.0m
                                            }
                                        }
                                    }
                                };
                            }

                            // Parse printer_groups if they exist
                            if (productElement.TryGetProperty("printer_groups", out var printerGroupsElement) && printerGroupsElement.ValueKind == JsonValueKind.Array)
                            {
                                var printerGroups = new List<Models.PrinterGroupModel>();
                                foreach (var printerGroupElement in printerGroupsElement.EnumerateArray())
                                {
                                    var printerGroup = new Models.PrinterGroupModel
                                    {
                                        Id = printerGroupElement.TryGetProperty("id", out var pgIdElement) ? pgIdElement.GetInt32() : 0,
                                        Name = printerGroupElement.TryGetProperty("name", out var pgNameElement) ? pgNameElement.GetString() : null,
                                        Description = printerGroupElement.TryGetProperty("description", out var pgDescElement) ? pgDescElement.GetString() : null,
                                        Status = printerGroupElement.TryGetProperty("status", out var pgStatusElement) && 
                                                (pgStatusElement.ValueKind == JsonValueKind.True || 
                                                 (pgStatusElement.ValueKind == JsonValueKind.Number && pgStatusElement.GetInt32() == 1) ||
                                                 (pgStatusElement.ValueKind == JsonValueKind.String && string.Equals(pgStatusElement.GetString(), "1", StringComparison.OrdinalIgnoreCase)))
                                    };
                                    
                                    
                                    printerGroups.Add(printerGroup);
                                }
                                product.PrinterGroups = printerGroups;
                                System.Diagnostics.Debug.WriteLine($"[ApiService] Parsed {printerGroups.Count} printer groups for product: {product.ItemName}");
                            }
                            else
                            {
                                // If no printer_groups, set empty list
                                product.PrinterGroups = new List<Models.PrinterGroupModel>();
                                System.Diagnostics.Debug.WriteLine($"[ApiService] No printer_groups found for product: {product.ItemName}");
                            }

                            products.Add(product);
                        }
                    }
                }

                return (categories, products);
            }
            catch (Exception ex)
            {
    
                throw;
            }
        }

        /* public async Task<List<string>> GetCategoriesAsync()
         {
             try
             {
                 // Set bearer token from settings
                 var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                 if (!string.IsNullOrEmpty(accessToken))
                 {
                     SetBearerToken(accessToken);
                 }

                 // Get the selected menu from shop details
                 var shopDetails = GlobalDataService.Instance.ShopDetails;
                 var selectedMenu = shopDetails?.DeliveryPlatform.SelectedMenu ?? 65; // Fallback to 65 if shop details not available

                 // Get brand ID from settings and shop ID from shop details
                 var (_, _, brandId) = _settingsService.LoadSettings();
                 var shopId = shopDetails?.Id ?? 3; // Fallback to 3 if shop details not available


                 var response = await _httpClient.GetAsync($"/api/v1/main-menu/{selectedMenu}/categories/webshop-brand/{brandId}/shop/{shopId}");
                 if (!response.IsSuccessStatusCode)
                 {
                     var error = await response.Content.ReadAsStringAsync();

                     throw new Exception($"Status: {response.StatusCode}\n{error}");
                 }

                 var json = await response.Content.ReadAsStringAsync();


                 using var doc = JsonDocument.Parse(json);
                 var data = doc.RootElement.GetProperty("data");
                 var categories = new List<string>();

                 // The API returns categories as object keys, not as array elements
                 // Each key is a category name and the value is an array of products
                 foreach (var property in data.EnumerateObject())
                 {
                     var categoryName = property.Name;
                     if (!string.IsNullOrEmpty(categoryName))
                     {
                         categories.Add(categoryName);

                     }
                 }


                 return categories;
             }
             catch (Exception ex)
             {

                 throw; // Re-throw to be handled by the calling method
             }
         }*/

        public async Task<List<POS_UI.Models.CustomerModel>> GetCustomersAsync()
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var response = await _httpClient.GetAsync("/api/v1/customers/");
                
                // If unauthorized, try to refresh token
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var refreshToken = POS_UI.Properties.Settings.Default.RefreshToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        try
                        {
                            var (newAccessToken, newRefreshToken, _, _) = await RefreshTokenAsync(refreshToken);
                            
                            // Save new tokens
                            POS_UI.Properties.Settings.Default.AccessToken = newAccessToken;
                            POS_UI.Properties.Settings.Default.RefreshToken = newRefreshToken;
                            POS_UI.Properties.Settings.Default.Save();
                            
                            // Retry the request with new token
                            SetBearerToken(newAccessToken);
                            response = await _httpClient.GetAsync("/api/v1/customers/");
                        }
                        catch (Exception refreshEx)
                        {
                            throw new Exception("Token refresh failed. Please log in again.");
                        }
                    }
                    else
                    {
                        throw new Exception("No refresh token available. Please log in again.");
                    }
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                var customers = new List<POS_UI.Models.CustomerModel>();
                foreach (var custElem in data.EnumerateArray())
                {
                    var customer = new POS_UI.Models.CustomerModel
                    {
                        FirstName = custElem.GetProperty("first_name").GetString(),
                        LastName = custElem.GetProperty("last_name").GetString(),
                        Phone = custElem.GetProperty("phone").GetString(),
                        CountryCode = custElem.TryGetProperty("country_code", out var cc) ? cc.GetString() : null,
                        CustomerId = custElem.GetProperty("id").GetInt32(),
                        Address = null,
                    };

                    if (custElem.TryGetProperty("addresses", out var addressesProp) && addressesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var a in addressesProp.EnumerateArray())
                        {
                            var addrModel = new POS_UI.Models.CustomerAddressModel
                            {
                                Id = a.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0,
                                Label = a.TryGetProperty("label", out var lp) ? lp.GetString() : null,
                                FlatNo = a.TryGetProperty("flat_no", out var fp) ? fp.GetString() : null,
                                HouseNo = a.TryGetProperty("house_no", out var hp) ? hp.GetString() : null,
                                AddressLine1 = a.TryGetProperty("address_line_1", out var a1) ? a1.GetString() : null,
                                AddressLine2 = a.TryGetProperty("address_line_2", out var a2) ? a2.GetString() : null,
                                Latitude = a.TryGetProperty("latitude", out var lat) ? lat.GetString() : null,
                                Longitude = a.TryGetProperty("longitude", out var lng) ? lng.GetString() : null,
                                City = a.TryGetProperty("city", out var city) ? city.GetString() : null,
                                Landmark = a.TryGetProperty("landmark", out var lm) ? lm.GetString() : null,
                                PostalCode = a.TryGetProperty("postal_code", out var pc) ? pc.GetString() : null,
                                IsDefault = a.TryGetProperty("default_address", out var def) && def.ValueKind == JsonValueKind.True
                            };
                            if (!string.IsNullOrWhiteSpace(addrModel.AddressLine1))
                                customer.Addresses.Add(addrModel);
                        }
                    }

                    customers.Add(customer);
                }
                return customers;
            }
            catch (Exception ex)
            {
    
                throw;
            }
        }

        public async Task<POS_UI.Models.CustomerDetailModel> GetCustomerByIdAsync(int customerId)
        {
            try
            {
                // Ensure bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var endpoint = $"/api/v1/customers/{customerId}";
                var response = await _httpClient.GetAsync(endpoint);

                // Handle token refresh on 401
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var refreshToken = POS_UI.Properties.Settings.Default.RefreshToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var (newAccessToken, newRefreshToken, _, _) = await RefreshTokenAsync(refreshToken);
                        POS_UI.Properties.Settings.Default.AccessToken = newAccessToken;
                        POS_UI.Properties.Settings.Default.RefreshToken = newRefreshToken;
                        POS_UI.Properties.Settings.Default.Save();
                        SetBearerToken(newAccessToken);
                        response = await _httpClient.GetAsync(endpoint);
                    }
                }

                var respBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on POST"); } catch { }
                    }
                    throw new Exception($"Status: {response.StatusCode}\n{respBody}");
                }

                using var doc = JsonDocument.Parse(respBody);
                var data = doc.RootElement.GetProperty("data");

                var customer = new POS_UI.Models.CustomerModel
                {
                    CustomerId = data.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                    FirstName = data.TryGetProperty("first_name", out var fn) ? fn.GetString() : string.Empty,
                    LastName = data.TryGetProperty("last_name", out var ln) ? ln.GetString() : string.Empty,
                    CountryCode = data.TryGetProperty("country_code", out var cc) ? cc.GetString() : string.Empty,
                    Phone = data.TryGetProperty("phone", out var ph) ? ph.GetString() : string.Empty,
                    Address = null
                };

                customer.Addresses = new List<POS_UI.Models.CustomerAddressModel>();
                if (data.TryGetProperty("addresses", out var addressesProp) && addressesProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var a in addressesProp.EnumerateArray())
                    {
                        var addrModel = new POS_UI.Models.CustomerAddressModel
                        {
                            Id = a.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0,
                            Label = a.TryGetProperty("label", out var lp) ? lp.GetString() : null,
                            FlatNo = a.TryGetProperty("flat_no", out var fp) ? fp.GetString() : null,
                            HouseNo = a.TryGetProperty("house_no", out var hp) ? hp.GetString() : null,
                            AddressLine1 = a.TryGetProperty("address_line_1", out var a1) ? a1.GetString() : null,
                            AddressLine2 = a.TryGetProperty("address_line_2", out var a2) ? a2.GetString() : null,
                            Latitude = a.TryGetProperty("latitude", out var lat) ? lat.GetString() : null,
                            Longitude = a.TryGetProperty("longitude", out var lng) ? lng.GetString() : null,
                            City = a.TryGetProperty("city", out var city) ? city.GetString() : null,
                            Landmark = a.TryGetProperty("landmark", out var lm) ? lm.GetString() : null,
                            PostalCode = a.TryGetProperty("postal_code", out var pc) ? pc.GetString() : null,
                            IsDefault = a.TryGetProperty("default_address", out var def) && def.ValueKind == JsonValueKind.True
                        };
                        customer.Addresses.Add(addrModel);
                    }
                }

                var orders = new List<POS_UI.Models.OrderModel>();
                if (data.TryGetProperty("orders", out var ordersProp) && ordersProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in ordersProp.EnumerateArray())
                    {
                        var order = new POS_UI.Models.OrderModel
                        {
                            ApiId = o.TryGetProperty("id", out var oid) ? oid.GetInt32() : 0,
                            DisplayOrderId = o.TryGetProperty("display_order_id", out var disp) ? disp.GetString() : string.Empty,
                            ShippingMethod = o.TryGetProperty("shipping_method", out var ship) ? ship.GetString() : string.Empty,
                            CustomerId = o.TryGetProperty("customer_id", out var cid) ? cid.GetInt32() : 0,
                            CustomerName = o.TryGetProperty("customer_name", out var cname) ? cname.GetString() : string.Empty
                        };
                        if (o.TryGetProperty("delivery_date_time", out var ddt) && ddt.ValueKind == JsonValueKind.String)
                        {
                            if (DateTime.TryParse(ddt.GetString(), out var parsed))
                            {
                                order.DeliveryDateTime = parsed.ToLocalTime();
                            }
                        }
                        orders.Add(order);
                    }
                }

                return new POS_UI.Models.CustomerDetailModel
                {
                    Customer = customer,
                    Orders = orders
                };
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> CreateCustomerAddressAsync(int customerId, POS_UI.Models.CustomerAddressModel address)
        {
            try
            {
                // Ensure bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var payload = new
                {
                    label = address?.Label ?? string.Empty,
                    // Send apartment/street number in flat_no (fallback from HouseNo if FlatNo empty)
                    flat_no = string.IsNullOrWhiteSpace(address?.FlatNo) ? (address?.HouseNo ?? string.Empty) : address.FlatNo,
                    // Do not send apartment in house_no
                    house_no = string.Empty,
                    address_line_1 = address?.AddressLine1 ?? string.Empty,
                    address_line_2 = address?.AddressLine2 ?? string.Empty,
                    latitude = address?.Latitude ?? string.Empty,
                    longitude = address?.Longitude ?? string.Empty,
                    city = address?.City ?? string.Empty,
                    landmark = address?.Landmark ?? string.Empty,
                    postal_code = address?.PostalCode ?? string.Empty,
                    default_address = address?.IsDefault ?? false
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var endpoint = $"/api/v1/customers/{customerId}/addresses";
                var response = await _httpClient.PostAsync(endpoint, content);

                // Handle token refresh on 401
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var refreshToken = POS_UI.Properties.Settings.Default.RefreshToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var (newAccessToken, newRefreshToken, _, _) = await RefreshTokenAsync(refreshToken);
                        POS_UI.Properties.Settings.Default.AccessToken = newAccessToken;
                        POS_UI.Properties.Settings.Default.RefreshToken = newRefreshToken;
                        POS_UI.Properties.Settings.Default.Save();
                        SetBearerToken(newAccessToken);
                        content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        response = await _httpClient.PostAsync(endpoint, content);
                    }
                }

                var respBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Status: {response.StatusCode}\n{respBody}");
                }

                // Optional: check { code: 200 }
                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    if (doc.RootElement.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.Number)
                    {
                        var codeVal = codeProp.GetInt32();
                        return codeVal == 200;
                    }
                }
                catch { /* ignore parse issues, treat success as true */ }
                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<bool> UpdateCustomerAsync(int customerId, POS_UI.Models.CustomerUpdateRequestModel updateRequest)
        {
            try
            {
                // Ensure bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                // Prepare the request payload
                var addresses = new List<object>();
                foreach (var address in updateRequest.Addresses)
                {
                    addresses.Add(new
                    {
                        id = address.Id,
                        label = address.Label ?? string.Empty,
                        flat_no = address.FlatNo ?? string.Empty,
                        house_no = address.HouseNo ?? string.Empty,
                        address_line_1 = address.AddressLine1 ?? string.Empty,
                        address_line_2 = address.AddressLine2 ?? string.Empty,
                        latitude = address.Latitude ?? string.Empty,
                        longitude = address.Longitude ?? string.Empty,
                        city = address.City ?? string.Empty,
                        landmark = address.Landmark ?? string.Empty,
                        postal_code = address.PostalCode ?? string.Empty,
                        default_address = address.IsDefault
                    });
                }

                var payload = new
                {
                    first_name = updateRequest.FirstName,
                    last_name = updateRequest.LastName,
                    country_code = updateRequest.CountryCode,
                    phone = updateRequest.Phone,
                    addresses = addresses
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var endpoint = $"/api/v1/customers/{customerId}";
                var response = await _httpClient.PutAsync(endpoint, content);

                // Handle token refresh on 401
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    var refreshToken = POS_UI.Properties.Settings.Default.RefreshToken;
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var (newAccessToken, newRefreshToken, _, _) = await RefreshTokenAsync(refreshToken);
                        POS_UI.Properties.Settings.Default.AccessToken = newAccessToken;
                        POS_UI.Properties.Settings.Default.RefreshToken = newRefreshToken;
                        POS_UI.Properties.Settings.Default.Save();
                        SetBearerToken(newAccessToken);
                        content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                        response = await _httpClient.PutAsync(endpoint, content);
                    }
                }

                var respBody = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Status: {response.StatusCode}\n{respBody}");
                }

                // Check for success response
                try
                {
                    using var doc = JsonDocument.Parse(respBody);
                    if (doc.RootElement.TryGetProperty("code", out var codeProp) && codeProp.ValueKind == JsonValueKind.Number)
                    {
                        var codeVal = codeProp.GetInt32();
                        return codeVal == 200;
                    }
                }
                catch { /* ignore parse issues, treat success as true */ }
                return true;
            }
            catch
            {
                throw;
            }
        }

        // Google Places integrations
        public async Task<List<string>> GoogleGetPredictionsAsync(string input, string countryCode)
        {
            try
            {
                var settings = new SettingsService();
                var key = settings.LoadGoogleApiKey();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(input) || input.Length < 3)
                    return new List<string>();

                var cc2 = (!string.IsNullOrWhiteSpace(countryCode) && countryCode.Trim().Length == 2)
                    ? countryCode.Trim().ToUpper()
                    : string.Empty;
                var cc = string.IsNullOrEmpty(cc2) ? string.Empty : $"&components=country:{cc2}";
                // Keep request simple and let Google handle country scoping via components
                var url = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?key={Uri.EscapeDataString(key)}&input={Uri.EscapeDataString(input)}{cc}";
                var resp = await _httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var preds = new List<string>();
                if (doc.RootElement.TryGetProperty("predictions", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var p in arr.EnumerateArray())
                    {
                        var desc = p.TryGetProperty("description", out var d) ? d.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(desc)) preds.Add(desc);
                    }
                }
                return preds;
            }
            catch { return new List<string>(); }
        }

        public async Task<(string PlaceId, double Lat, double Lng, string FormattedAddress)> GoogleResolvePlaceAsync(string description, string countryCode = null)
        {
            try
            {
                var settings = new SettingsService();
                var key = settings.LoadGoogleApiKey();
                if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(description))
                    return (null, 0, 0, null);

                // First: Find place_id via autocomplete for the description
                var cc2 = (!string.IsNullOrWhiteSpace(countryCode) && countryCode.Trim().Length == 2) ? countryCode.Trim().ToUpper() : null;
                var cc = string.IsNullOrEmpty(cc2) ? string.Empty : $"&components=country:{cc2}";
                var urlAuto = $"https://maps.googleapis.com/maps/api/place/autocomplete/json?key={Uri.EscapeDataString(key)}&input={Uri.EscapeDataString(description)}{cc}";
                var respAuto = await _httpClient.GetAsync(urlAuto);
                respAuto.EnsureSuccessStatusCode();
                var jsonAuto = await respAuto.Content.ReadAsStringAsync();
                using var docAuto = System.Text.Json.JsonDocument.Parse(jsonAuto);
                string placeId = null;
                if (docAuto.RootElement.TryGetProperty("predictions", out var preds) && preds.GetArrayLength() > 0)
                {
                    placeId = preds[0].TryGetProperty("place_id", out var pid) ? pid.GetString() : null;
                }
                if (string.IsNullOrWhiteSpace(placeId)) return (null, 0, 0, description);

                // Details for place_id
                var urlDetails = $"https://maps.googleapis.com/maps/api/place/details/json?key={Uri.EscapeDataString(key)}&place_id={Uri.EscapeDataString(placeId)}";
                var respDetails = await _httpClient.GetAsync(urlDetails);
                respDetails.EnsureSuccessStatusCode();
                var jsonDetails = await respDetails.Content.ReadAsStringAsync();
                using var docDet = System.Text.Json.JsonDocument.Parse(jsonDetails);
                var result = docDet.RootElement.GetProperty("result");
                var location = result.GetProperty("geometry").GetProperty("location");
                double lat = location.GetProperty("lat").GetDouble();
                double lng = location.GetProperty("lng").GetDouble();

                // Prefer the details API's formatted_address and prefix with the place name when available
                string name = result.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                string detailsFormatted = result.TryGetProperty("formatted_address", out var detFa) ? detFa.GetString() : null;
                System.Diagnostics.Debug.WriteLine($"[Places] Details name='{name}', details_formatted='{detailsFormatted}'");

                string formatted = null;
                if (!string.IsNullOrWhiteSpace(detailsFormatted))
                {
                    formatted = detailsFormatted;
                    if (!string.IsNullOrWhiteSpace(name) && !formatted.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                    {
                        formatted = $"{name}, {formatted}";
                    }
                }
                else
                {
                    // Fallback: Reverse geocode to get formatted address
                var urlGeo = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={lat},{lng}&key={Uri.EscapeDataString(key)}";
                var respGeo = await _httpClient.GetAsync(urlGeo);
                respGeo.EnsureSuccessStatusCode();
                var jsonGeo = await respGeo.Content.ReadAsStringAsync();
                using var docGeo = System.Text.Json.JsonDocument.Parse(jsonGeo);
                if (docGeo.RootElement.TryGetProperty("results", out var resArr) && resArr.GetArrayLength() > 0)
                {
                    formatted = resArr[0].TryGetProperty("formatted_address", out var fa) ? fa.GetString() : null;
                    }
                }
                return (placeId, lat, lng, formatted ?? description);
            }
            catch { return (null, 0, 0, description); }
        }

        public async Task<string> GoogleGetDistanceTextAsync((double Lat, double Lng) origins, (double Lat, double Lng) destinations, string units = "imperial", string travelMode = "driving")
        {
            try
            {
                var settings = new SettingsService();
                var key = settings.LoadGoogleApiKey();
                if (string.IsNullOrWhiteSpace(key)) return string.Empty;
                var url = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={origins.Lat},{origins.Lng}&destinations={destinations.Lat},{destinations.Lng}&units={units}&mode={travelMode}&key={Uri.EscapeDataString(key)}";
                var resp = await _httpClient.GetAsync(url);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("rows", out var rows) && rows.GetArrayLength() > 0)
                {
                    var elements = rows[0].GetProperty("elements");
                    if (elements.GetArrayLength() > 0 && elements[0].TryGetProperty("distance", out var dist))
                    {
                        return dist.TryGetProperty("text", out var txt) ? txt.GetString() : string.Empty;
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public async Task<byte[]> ExportOrdersAsync(string platforms = "1,2,4,6,8,9", int? outletId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No access token found. Please log in first.");
                }

                // Get outlet_id from shop details if not provided
                var finalOutletId = outletId;
                if (!finalOutletId.HasValue || finalOutletId <= 0)
                {
                    var shopDetails = GlobalDataService.Instance.ShopDetails;
                    if (shopDetails != null)
                    {
                        if (shopDetails.DeliveryPlatform != null && shopDetails.DeliveryPlatform.OutletId > 0)
                        {
                            finalOutletId = shopDetails.DeliveryPlatform.OutletId;
                        }
                        else if (shopDetails.Id > 0)
                        {
                            finalOutletId = shopDetails.Id;
                        }
                    }
                }

                if (!finalOutletId.HasValue || finalOutletId <= 0)
                {
                    throw new Exception("Outlet ID is 0. Ensure shop details are loaded before calling ExportOrdersAsync.");
                }

                // Build the export URL
                var fromDateStr = fromDate?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var toDateStr = toDate?.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

                var url = $"/api/v1/orders/export?platforms={platforms}&outlet_id={finalOutletId}&from_date={fromDateStr}";
                if (!string.IsNullOrEmpty(toDateStr))
                {
                    url += $"&to_date={toDateStr}";
                }

                // Create a new HttpClient for this request to avoid conflicts with the main client
                using var exportHttpClient = new HttpClient();
                exportHttpClient.Timeout = TimeSpan.FromMinutes(10); // Longer timeout for file downloads
                exportHttpClient.BaseAddress = _httpClient.BaseAddress;
                exportHttpClient.DefaultRequestHeaders.Accept.Clear();
                exportHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/csv"));
                exportHttpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                exportHttpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Add tenant headers
                var (tenantCode, outletCode, brandId) = _settingsService.LoadSettings();
                exportHttpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                //exportHttpClient.DefaultRequestHeaders.Add("x-outlet-code", outletCode);
                /*if (!string.IsNullOrWhiteSpace(brandId))
                {
                    exportHttpClient.DefaultRequestHeaders.Add("x-brand-id", brandId);
                }*/

                var response = await exportHttpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsByteArrayAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Export orders error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<POS_UI.Models.OrderModel>> GetOrdersAsync(string status = "QUEUE", string platforms = "1,2,6,8,9", int? outletId = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No access token found. Please log in first.");
                }
                
                SetBearerToken(accessToken);

                // Get outlet_id from shop details and brand_id from settings
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                // Attempt to hydrate shop details if null (from local storage or API)
                if (shopDetails == null)
                {
                    try
                    {
                        var localStorage = new LocalStorageService();
                        var stored = localStorage.GetShopDetails();
                        if (stored != null)
                        {
                            shopDetails = stored;
                        }
                        else
                        {
                            var (_, outletCodeFromSettings, brandIdFromSettings) = _settingsService.LoadSettings();
                            if (!string.IsNullOrWhiteSpace(outletCodeFromSettings) && !string.IsNullOrWhiteSpace(brandIdFromSettings))
                            {
                                shopDetails = await GetShopDetailsAsync(outletCodeFromSettings, brandIdFromSettings);
                            }
                        }
                    }
                    catch { /* silent hydrate attempt */ }
                }


                var (_, _, brandId) = _settingsService.LoadSettings();
                
               // Use provided outletId if available, else derive from shopDetails
                var finalOutletId = outletId ?? 0;
                if (finalOutletId <= 0)
                {
                    if (shopDetails != null)
                    {
                        if (shopDetails.Id > 0)
                        {
                            finalOutletId = shopDetails.Id;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(brandId))
                {
                    throw new Exception("Brand ID not found in settings. Please check settings.txt file.");
                }
                if (finalOutletId <= 0)
                {
                    throw new Exception("Outlet ID is 0. Ensure shop details are loaded before calling GetOrdersAsync.");
                }

                // Build URL based on whether status is provided
                string url;
                if (string.IsNullOrEmpty(status))
                {
                    // Get orders without status filter, support date range
                    url = $"/api/v1/orders?platforms={platforms}&outlet_id={finalOutletId}";
                    if (fromDate.HasValue)
                    {
                        url += $"&from_date={fromDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}";
                        //MessageBox.Show($"from date: {fromDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                    }
                    if (toDate.HasValue)
                    {
                        url += $"&to_date={toDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}";
                        //MessageBox.Show($"to date: {toDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                    }
                }
                else
                {
                    // Get orders with specific status
                    url = $"/api/v1/orders?status={status}&platforms={platforms}&outlet_id={finalOutletId}";
                }
                var response = await _httpClient.GetAsync(url);
                //MessageBox.Show($"API Service: Making request to: {url}");
                //MessageBox.Show($"API Service: Brand ID: {brandId}");
                //MessageBox.Show($"API Service: Outlet ID: {outletId}");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetOrders"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{error}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                
                // Check if response has data property, otherwise use root element
                var dataElement = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
                var orders = new List<POS_UI.Models.OrderModel>();
                
                foreach (var orderElem in dataElement.EnumerateArray())
                {
                    try
                    {
                        var order = new POS_UI.Models.OrderModel
                        {
                            Id = Guid.NewGuid(), // Generate new Guid for API orders
                            ApiId = orderElem.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                            OrderNumber = orderElem.TryGetProperty("display_order_id", out var orderIdProp) ? orderIdProp.GetString() ?? "Unknown" : "Unknown",
                            DisplayOrderId = orderElem.TryGetProperty("display_order_id", out var displayOrderIdProp) ? displayOrderIdProp.GetString() ?? "Unknown" : "Unknown",
                            CreatedAt = orderElem.TryGetProperty("delivery_date_time", out var dateProp) && 
                                      DateTimeOffset.TryParse(dateProp.GetString(), out var dateOffset) ? dateOffset.DateTime : DateTime.Now,
                            CustomerName = orderElem.TryGetProperty("customer_name", out var customerNameProp) ? customerNameProp.GetString() ?? "" : "",
                            DiscountAmount = orderElem.TryGetProperty("discount", out var discountProp) ? discountProp.GetDecimal() : 0m,
                            DiscountPercentage = orderElem.TryGetProperty("discount_percentage", out var discountPercentProp) ? discountPercentProp.GetDecimal() : 0m,
                            OrderNotes = orderElem.TryGetProperty("note", out var noteProp) ? noteProp.GetString() ?? "" : 
                                        (orderElem.TryGetProperty("order_note", out var orderNoteProp) ? orderNoteProp.GetString() ?? "" : 
                                        (orderElem.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() ?? "" : "")),
                            Status = ParseOrderStatus(orderElem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : ""),
                            ApiStatus = orderElem.TryGetProperty("status", out var _statusProp2) ? _statusProp2.GetString() ?? string.Empty : string.Empty,
                            ApiTotal = orderElem.TryGetProperty("total_amount", out var totalProp) ? totalProp.GetDecimal() : (decimal?)null,
                            PlatformId = orderElem.TryGetProperty("platform_id", out var platformIdProp) ? platformIdProp.GetInt32() : 0,
                            PlatformName = orderElem.TryGetProperty("platform_name", out var platformNameProp) ? platformNameProp.GetString() ?? "Unknown" : "Unknown",
                            PlatformLogo = orderElem.TryGetProperty("platform_logo", out var platformLogoProp) ? platformLogoProp.GetString() ?? "Unknown" : "Unknown",
                            RemoteOrderId = orderElem.TryGetProperty("remote_order_id", out var remoteOrderIdProp) ? remoteOrderIdProp.GetString() : null,
                            PaymentStatus = orderElem.TryGetProperty("payment_status", out var paymentStatusProp) ? paymentStatusProp.GetString() ?? "" : "",
                            PaymentMethod = orderElem.TryGetProperty("payment_method", out var paymentMethodProp) ? paymentMethodProp.GetString() ?? "" : "",
                            ShippingMethod = orderElem.TryGetProperty("shipping_method", out var shippingMethodProp) ? shippingMethodProp.GetString() ?? "" : "",
                            DeliveryPlatfornName = orderElem.TryGetProperty("delivery_platform_name", out var deliveryPlatformNameProp) ? deliveryPlatformNameProp.GetString() ?? "Unknown" : "Unknown",
                            TableNumber = orderElem.TryGetProperty("table_id", out var tableNumberProp) ? tableNumberProp.GetInt32() : 0,
                            UserShiftId = orderElem.TryGetProperty("user_shift_id", out var userShiftIdProp) ? userShiftIdProp.GetInt32() : 0,
                            CashDrawerSessionId = orderElem.TryGetProperty("cash_drawer_session_id", out var cashDrawerSessionIdProp) ? cashDrawerSessionIdProp.GetInt32() : 0,
                            OrderSessionId = orderElem.TryGetProperty("order_session_id", out var orderSessionIdProp) ? orderSessionIdProp.GetInt32() : 0,
                            TableOrderingsId = orderElem.TryGetProperty("table_orderings_id", out var tableOrderingsIdProp) ? tableOrderingsIdProp.GetInt32() : 0,
                            CreatedAtActual = orderElem.TryGetProperty("created_at", out var createdAtActualProp) ? DateTime.Parse(createdAtActualProp.GetString()).ToLocalTime() : DateTime.MinValue,
                            TableOrderMethod = orderElem.TryGetProperty("table_order_method", out var tableOrderMethodProp) ? tableOrderMethodProp.GetString() ?? "" : "",
                            TableName = orderElem.TryGetProperty("table_name", out var tableNameProp) ? tableNameProp.GetString() ?? null : null
                        };

                        // Set Platform name based on PlatformId
                        /*order.Platform = order.PlatformId switch
                        {
                            1 => "Deliveroo",
                            2 => "UberEats",
                            6 => "Webshop",
                            8 => "Table Order",
                            9 => "DG POS",
                            _ => "DG POS" // Default fallback
                        };*/

                    // Parse order type
                    var shippingMethod = orderElem.TryGetProperty("shipping_method", out var shippingProp) ? shippingProp.GetString() ?? "" : "";
                    order.OrderType = shippingMethod switch
                    {
                        "DINE-IN" => Models.OrderType.DineIn,
                        "DELIVERY" => Models.OrderType.Delivery,
                        "TAKEAWAY" => Models.OrderType.TakeAway,
                        "COLLECTION" => Models.OrderType.Collection,
                        _ => Models.OrderType.TakeAway
                    };

                    // Parse table number for dine-in orders
                    if (order.OrderType == Models.OrderType.DineIn)
                    {
                        if (orderElem.TryGetProperty("table_id", out var tableIdElement))
                        {
                            order.TableNumber = tableIdElement.GetInt32();
                        }
                    }
                    orders.Add(order);
                    }
                    catch (Exception orderEx)
                    {
                        // Skip problematic orders and continue
                        System.Diagnostics.Debug.WriteLine($"Error parsing order: {orderEx.Message}");
                    }
                }
                
                return orders;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private Models.OrderStatus ParseOrderStatus(string status)
        {
            return status?.ToUpper() switch
            {
                "QUEUE" => Models.OrderStatus.Draft,
                "ACCEPTED" => Models.OrderStatus.Draft,
                "PREPARING" => Models.OrderStatus.Draft,
                "READY" => Models.OrderStatus.Ready,
                "SERVED" => Models.OrderStatus.Served,
                "DELIVERED" => Models.OrderStatus.Served,
                "COMPLETED" => Models.OrderStatus.Served,
                "TEMP" => Models.OrderStatus.Draft,
                _ => Models.OrderStatus.Draft
            };
        }

        public async Task<POS_UI.Models.CustomerModel> CreateCustomerAsync(string name, string countryCode, string phone)
        {
            try
            {   
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }
                else
                {
                    //MessageBox.Show("Warning: No access token found");
                }

                // Create request body
                var requestBody = new
                {
                    name = name,
                    country_code = countryCode,
                    phone_number = phone
                };

                var json = JsonSerializer.Serialize(requestBody);
                System.Diagnostics.Debug.WriteLine($"[CreateCustomerAsync] Request JSON: {json}");
                // Test JSON parsing to ensure it's valid
                try
                {
                    using var testDoc = JsonDocument.Parse(json);
                }
                catch (Exception jsonEx)
                {
                    throw new Exception($"Invalid JSON generated: {jsonEx.Message}");
                }
                
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/api/v1/customers", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Check if response is empty
                if (string.IsNullOrWhiteSpace(responseBody))
                {
                    throw new Exception("Empty response received from server");
                }

                // Check if response looks like JSON
                /*var trimmedResponse = responseBody.Trim();
                if (!trimmedResponse.StartsWith("{") && !trimmedResponse.StartsWith("["))
                {
                    throw new Exception($"Response is not JSON format. Response starts with: {trimmedResponse.Substring(0, Math.Min(50, trimmedResponse.Length))}");
                }*/
                
                if (!response.IsSuccessStatusCode)
                {
                    // Try to extract friendly validation messages for phone number
                    if ((int)response.StatusCode == 400)
                    {
                        try
                        {
                            using var errDoc = JsonDocument.Parse(responseBody);
                            // Common API error formats: { errors: { PhoneNumber: ["..."] } } or { message: "..." }
                            if (errDoc.RootElement.TryGetProperty("errors", out var errorsEl))
                            {
                                string friendly = null;
                                if (errorsEl.TryGetProperty("PhoneNumber", out var phoneErrors) && phoneErrors.ValueKind == JsonValueKind.Array && phoneErrors.GetArrayLength() > 0)
                                {
                                    friendly = phoneErrors[0].GetString();
                                }
                                else if (errorsEl.TryGetProperty("phone_number", out var phoneErrors2) && phoneErrors2.ValueKind == JsonValueKind.Array && phoneErrors2.GetArrayLength() > 0)
                                {
                                    friendly = phoneErrors2[0].GetString();
                                }
                                if (!string.IsNullOrWhiteSpace(friendly))
                                {
                                    throw new Exception(friendly);
                                }
                            }
                            if (errDoc.RootElement.TryGetProperty("message", out var msgEl))
                            {
                                var message = msgEl.GetString();
                                if (!string.IsNullOrWhiteSpace(message))
                                {
                                    throw new Exception(message);
                                }
                            }
                        }
                        catch
                        {
                            // Fall back to generic below
                        }
                    }
                    throw new Exception($"Failed to create customer. Status: {response.StatusCode}\n{responseBody}");
                }

                // Try to parse the response as JSON
                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(responseBody);
                }
                catch (Exception parseEx)
                {
                    throw new Exception($"Invalid JSON response from server: {parseEx.Message}\nResponse: {responseBody}");
                }

                // Check if response indicates success
                if (doc.RootElement.TryGetProperty("code", out var codeElement) && 
                    codeElement.GetInt32() == 200)
                {
                    // API returned success, but no customer data in response
                    // Create a customer object from the input parameters
                    var nameParts = name?.Split(' ', 2) ?? new string[0];
                    var firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                    var customer = new POS_UI.Models.CustomerModel
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = phone,
                        CountryCode = countryCode,
                        CustomerId = 0 // API didn't return an ID, we'll try to resolve below
                    };
                    try
                    {
                        // Best-effort: fetch customers and match by phone to resolve the ID immediately
                        var allCustomers = await GetCustomersAsync();
                        var match = allCustomers.FirstOrDefault(c =>
                            (!string.IsNullOrWhiteSpace(c.Phone) && string.Equals(c.Phone.Trim(), phone?.Trim(), StringComparison.OrdinalIgnoreCase)) ||
                            (!string.IsNullOrWhiteSpace(c.FullPhoneNumber) && string.Equals(c.FullPhoneNumber.Trim(), ($"{countryCode}{phone}").Trim(), StringComparison.OrdinalIgnoreCase))
                        );
                        if (match != null && match.CustomerId > 0)
                        {
                            customer.CustomerId = match.CustomerId;
                            customer.Addresses = match.Addresses;
                        }
                    }
                    catch { /* ignore resolution errors; caller can re-resolve if needed */ }
                    
                    // Success: no UI alert needed; caller will handle closing dialogs and updating state
                    return customer;
                }
                else if (doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    // Handle case where API returns data property (legacy format)
                    var data = dataElement;

                    // Parse the name into first and last name
                    var fullName = data.GetProperty("name").GetString();
                    var nameParts = fullName?.Split(' ', 2) ?? new string[0];
                    var firstName = nameParts.Length > 0 ? nameParts[0] : "";
                    var lastName = nameParts.Length > 1 ? nameParts[1] : "";

                    var customer = new POS_UI.Models.CustomerModel
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Phone = data.GetProperty("phone_number").GetString(),
                        CountryCode = data.GetProperty("country_code").GetString(),
                        CustomerId = data.GetProperty("id").GetInt32()
                    };
                    
                    // Success: no UI alert needed; caller will handle closing dialogs and updating state
                    return customer;
                }
                else
                {
                    throw new Exception($"Unexpected response structure. Response: {responseBody}");
                }
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    throw;
                }
                
                //MessageBox.Show($"Error in CreateCustomerAsync: {ex.Message}");
                //MessageBox.Show($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task<bool> UpdateOrderStatusAsync(int orderId, string status, string note = null)
        {
            try
            {
                // Check if user is logged in
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to update order status.");
                }
                
                SetBearerToken(accessToken);

                // Create the request body
                var requestBody = new Dictionary<string, object>
                {
                    { "status", status }
                };
                
                // Add note if provided
                if (!string.IsNullOrWhiteSpace(note))
                {
                    requestBody["note"] = note;
                }

                // Serialize the request body to JSON
                string json;
                try
                {
                    json = JsonSerializer.Serialize(requestBody);
                }
                catch (Exception jsonEx)
                {
                    throw new Exception($"Invalid JSON generated: {jsonEx.Message}");
                }
                
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                // Make PATCH request to update order status
                var response = await _httpClient.PatchAsync($"/api/v1/orders/{orderId}/status", content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    throw;
                }
                
                //MessageBox.Show($"Error updating order status: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateTableStatusAsync(int tableId, string status, int ongoingOrderId)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to update table status.");
                }
                SetBearerToken(accessToken);

                var requestBody = new
                {
                    table_id = tableId,
                    status = status,
                    ongoing_order_id = ongoingOrderId
                };

                string json;
                try
                {
                    json = JsonSerializer.Serialize(requestBody);
                }
                catch (Exception jsonEx)
                {
                    throw new Exception($"Invalid JSON generated: {jsonEx.Message}");
                }

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync("/api/v1/tables/status", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");

                if (isNetworkError)
                {
                    throw;
                }

                //MessageBox.Show($"Error updating table status: {ex.Message}");
                throw;
            }
        }

        public async Task<List<POS_UI.Models.TableModel>> GetTablesAsync()
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to get tables.");
                }
                SetBearerToken(accessToken);
                
                // Get outlet_id from shop details and brand_id from settings
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                // Attempt to hydrate shop details if null (from local storage or API)
                if (shopDetails == null)
                {
                    try
                    {
                        var localStorage = new LocalStorageService();
                        var stored = localStorage.GetShopDetails();
                        if (stored != null)
                        {
                            shopDetails = stored;
                        }
                        else
                        {
                            var (_, outletCodeFromSettings, brandIdFromSettings) = _settingsService.LoadSettings();
                            if (!string.IsNullOrWhiteSpace(outletCodeFromSettings) && !string.IsNullOrWhiteSpace(brandIdFromSettings))
                            {
                                shopDetails = await GetShopDetailsAsync(outletCodeFromSettings, brandIdFromSettings);
                            }
                        }
                    }
                    catch { /* silent hydrate attempt */ }
                }


                var (_, _, brandId) = _settingsService.LoadSettings();
                
                // Prefer DeliveryPlatform.OutletId if provided, else Shop Id
                var outletId = 0;
                if (shopDetails != null)
                {
                    if (shopDetails.DeliveryPlatform != null && shopDetails.DeliveryPlatform.OutletId > 0)
                    {
                        outletId = shopDetails.DeliveryPlatform.OutletId;
                    }
                    else if (shopDetails.Id > 0)
                    {
                        outletId = shopDetails.Id;
                    }
                }

                
                if (string.IsNullOrEmpty(brandId))
                {
                    throw new Exception("Brand ID not found in settings. Please check settings.txt file.");
                }
                if (outletId <= 0)
                {
                    throw new Exception("Outlet ID is 0. Ensure shop details are loaded before calling GetPlatformsAsync.");
                }


                var url = $"/api/v1/tables?outlet_id={outletId}&brand_id={brandId}";
                 //MessageBox.Show($"API Service: Making request to: {url}");
                //MessageBox.Show($"API Service: Brand ID: {brandId}");
                //MessageBox.Show($"API Service: Outlet ID: {outletId}");
                var response = await _httpClient.GetAsync(url);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetTables"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                var tables = new List<POS_UI.Models.TableModel>();
                using var doc = JsonDocument.Parse(responseBody);
                var dataArray = doc.RootElement.GetProperty("data");
                
                foreach (var tableElem in dataArray.EnumerateArray())
                {
                    var table = new POS_UI.Models.TableModel
                    {
                        ApiId = tableElem.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                        Name = tableElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? "" : "",
                        Description = tableElem.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? "" : "",
                        SeatCount = tableElem.TryGetProperty("seat_count", out var seatProp) ? seatProp.GetInt32() : 0,
                        ShopId = tableElem.TryGetProperty("shop_id", out var shopProp) ? shopProp.GetInt32() : 0,
                        BrandId = tableElem.TryGetProperty("brand_id", out var brandProp) ? brandProp.GetInt32() : 0,
                        Status = ParseTableStatus(tableElem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : ""),
                        CreatedAt = tableElem.TryGetProperty("created_at", out var createdProp) ? DateTime.Parse(createdProp.GetString()).ToLocalTime() : DateTime.MinValue,
                        UpdatedAt = tableElem.TryGetProperty("updated_at", out var updatedProp) ? DateTime.Parse(updatedProp.GetString()).ToLocalTime() : DateTime.MinValue,
                        TableOrderingsId = tableElem.TryGetProperty("table_orderings_id", out var tableOrderingsIdProp) ? tableOrderingsIdProp.GetInt32() : 0
                    };

                    // Set TableNumber based on Name (e.g., "T1" -> 1, "T2" -> 2)
                    if (int.TryParse(table.Name.Replace("T", ""), out int tableNumber))
                    {
                        table.TableNumber = tableNumber;
                    }
                    else
                    {
                        table.TableNumber = table.ApiId; // Fallback to API ID
                    }

                    // Parse order information: prefer ongoing_orders array, fallback to single order
                    if (tableElem.TryGetProperty("ongoing_orders", out var ongoingOrdersProp) && ongoingOrdersProp.ValueKind == JsonValueKind.Array)
                    {
                        var arr = ongoingOrdersProp.EnumerateArray();
                        if (arr.MoveNext())
                        {
                            table.Order = ParseOrderFromOngoingSummary(arr.Current);
                            table.Amount = table.Order.DisplayTotal;
                        }
                    }
                    tables.Add(table);
                }

                return tables;
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    throw;
                }
                
                //MessageBox.Show($"Error getting tables: {ex.Message}");
                throw;
            }
        }

        private POS_UI.Models.OrderModel ParseOrderFromTable(JsonElement orderElem)
        {
            var order = new POS_UI.Models.OrderModel
            {
                Id = Guid.NewGuid(),
                ApiId = orderElem.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                OrderNumber = orderElem.TryGetProperty("display_order_id", out var orderIdProp) ? orderIdProp.GetString() ?? "Unknown" : "Unknown",
                CustomerName = orderElem.TryGetProperty("customer_name", out var customerProp) ? customerProp.GetString() ?? "" : "",
                ApiTotal = orderElem.TryGetProperty("total_amount", out var totalProp) ? totalProp.GetDecimal() : 0m,
                // Note: We don't set Subtotal here as it's calculated from Total - DiscountAmount - CouponAmount
                Status = ParseOrderStatus(orderElem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : ""),
                ApiStatus = orderElem.TryGetProperty("status", out var _statusProp2) ? _statusProp2.GetString() ?? string.Empty : string.Empty,
                OrderType = ParseOrderType(orderElem.TryGetProperty("shipping_method", out var shippingProp) ? shippingProp.GetString() ?? "" : ""),
                TableNumber = orderElem.TryGetProperty("table_id", out var tableProp) ? tableProp.GetInt32() : 0,
                OrderNotes = orderElem.TryGetProperty("note", out var noteProp) ? noteProp.GetString() ?? "" : 
                            (orderElem.TryGetProperty("order_note", out var orderNoteProp) ? orderNoteProp.GetString() ?? "" : 
                            (orderElem.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() ?? "" : "")),
                CreatedAt = orderElem.TryGetProperty("created_at", out var createdProp) ? DateTime.Parse(createdProp.GetString()).ToLocalTime() : DateTime.MinValue,
                UpdatedAt = orderElem.TryGetProperty("updated_at", out var updatedProp) ? DateTime.Parse(updatedProp.GetString()).ToLocalTime() : DateTime.MinValue,
                PlatformId = orderElem.TryGetProperty("platform_id", out var platformIdProp) ? platformIdProp.GetInt32() : 0,
                DiscountModeApplied = orderElem.TryGetProperty("discount_mode_applied", out var discountModeProp) ? discountModeProp.GetString() ?? "percentage" : "percentage",
                // Parse discount value based on mode
                                DiscountAmount = orderElem.TryGetProperty("discount", out var discountProp) ? discountProp.GetDecimal() : 0m,
                DiscountPercentage = orderElem.TryGetProperty("discount_percentage", out var discountPercentProp) ? discountPercentProp.GetDecimal() : 0m,
                UserShiftId = orderElem.TryGetProperty("user_shift_id", out var userShiftIdProp) ? userShiftIdProp.GetInt32() : (int?)null,
                OrderSessionId = orderElem.TryGetProperty("order_session_id", out var orderSessionIdProp) ? orderSessionIdProp.GetInt32() : 0,
                IsTableOrder = orderElem.TryGetProperty("is_table_order", out var isTableOrderProp) ? isTableOrderProp.GetBoolean() : false,
            };

            // Set Platform name based on PlatformId
            /*order.Platform = order.PlatformId switch
            {
                1 => "Deliveroo",
                2 => "UberEats",
                6 => "Webshop",
                8 => "Table Order",
                9 => "DG POS",
                _ => "DG POS" // Default fallback
            };*/

            return order;
        }

        /// <summary>Parse a single order from the tables API <c>ongoing_orders</c> array (align fields with list/detail order JSON where possible).</summary>
        private POS_UI.Models.OrderModel ParseOrderFromOngoingSummary(JsonElement orderElem)
        {
            var orderId = orderElem.TryGetProperty("order_id", out var idProp) ? idProp.GetInt32()
                : (orderElem.TryGetProperty("id", out var idProp2) ? idProp2.GetInt32() : 0);
            var orderSessionIdRaw = orderElem.TryGetProperty("order_session_id", out var sessionProp) ? sessionProp.GetInt32() : 0;
            int? orderSessionId = orderSessionIdRaw > 0 ? orderSessionIdRaw : null;

            var displayRaw = orderElem.TryGetProperty("display_order_id", out var orderIdProp) ? orderIdProp.GetString() ?? "" : "";
            var orderNumber = string.IsNullOrWhiteSpace(displayRaw) ? "Unknown" : displayRaw;

            DateTime? deliveryDateTime = null;
            if (orderElem.TryGetProperty("delivery_date_time", out var ddtEl) && ddtEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(ddtEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var ddtParsed))
                deliveryDateTime = ddtParsed.ToLocalTime();

            DateTime? scheduledTime = null;
            if (orderElem.TryGetProperty("scheduled_time", out var stEl) && stEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(stEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var stParsed))
                scheduledTime = stParsed.ToLocalTime();

            DateTime createdAt = default;
            if (orderElem.TryGetProperty("created_at", out var crEl) && crEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(crEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var crParsed))
                createdAt = crParsed.ToLocalTime();
            else if (deliveryDateTime.HasValue)
                createdAt = deliveryDateTime.Value;
            else
                createdAt = DateTime.Now;

            var shippingMethod = TryGetOrderStringInsensitive(orderElem, "shipping_method", "shippingMethod", "order_type", "orderType");

            var paymentStatus = TryGetPaymentStatusFromOngoingOrder(orderElem, out var hasIsPaid, out var isPaidFlag);

            var platformLogo = TryGetOrderStringInsensitive(orderElem, "platform_logo", "platformLogo");
            if (string.IsNullOrWhiteSpace(platformLogo) || string.Equals(platformLogo, "Unknown", StringComparison.OrdinalIgnoreCase))
                platformLogo = "";

            // Match Live Orders list API: primary platform is platform_id (table=8, POS=9). Keep delivery_platform_id on PlatformId2.
            var platformId = TryGetOrderInt32(orderElem, "platform_id", "platformId");
            var deliveryPlatformId = TryGetOrderInt32(orderElem, "delivery_platform_id", "deliveryPlatformId");
            var platformId2 = deliveryPlatformId;
            if (platformId == 0)
                platformId = deliveryPlatformId;

            if (string.IsNullOrWhiteSpace(platformLogo) && (platformId == 9 || platformId2 == 9))
            {
                var shop = GlobalDataService.Instance.ShopDetails;
                if (!string.IsNullOrWhiteSpace(shop?.ShopLogo))
                    platformLogo = shop.ShopLogo;
            }

            var totalAmount = TryGetOrderDecimalInsensitive(orderElem, "total_amount", "totalAmount");

            var order = new POS_UI.Models.OrderModel
            {
                Id = Guid.NewGuid(),
                ApiId = orderId,
                OrderNumber = orderNumber,
                DisplayOrderId = string.IsNullOrWhiteSpace(displayRaw) || string.Equals(displayRaw, "Unknown", StringComparison.Ordinal) ? orderNumber : displayRaw,
                CustomerName = orderElem.TryGetProperty("customer_name", out var customerProp) ? customerProp.GetString() ?? "" : "",
                ApiTotal = totalAmount,
                Status = ParseOrderStatus(orderElem.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : ""),
                ApiStatus = orderElem.TryGetProperty("status", out var statusProp2) ? statusProp2.GetString() ?? string.Empty : string.Empty,
                OrderSessionId = orderSessionId,
                IsTableOrder = TryGetJsonBoolean(orderElem, "is_table_order", "isTableOrder"),
                CreatedAt = createdAt,
                DeliveryDateTime = deliveryDateTime,
                ScheduledTime = scheduledTime,
                PaymentStatus = paymentStatus,
                IsPaid = string.Equals(paymentStatus, "PAID", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(paymentStatus, "Paid", StringComparison.Ordinal)
                    || (hasIsPaid && isPaidFlag),
                PlatformLogo = platformLogo,
                PlatformName = TryGetOrderStringInsensitive(orderElem, "platform_name", "platformName"),
                DeliveryPlatfornName = TryGetOrderStringInsensitive(orderElem, "delivery_platform_name", "deliveryPlatformName"),
                PlatformId = platformId,
                PlatformId2 = platformId2,
                ShippingMethod = shippingMethod,
                TableOrderMethod = TryGetOrderStringInsensitive(orderElem, "table_order_method", "tableOrderMethod"),
                TableNumber = orderElem.TryGetProperty("table_id", out var tid) ? tid.GetInt32() : 0,
                TableName = orderElem.TryGetProperty("table_name", out var tnm) ? tnm.GetString() : null,
                OrderType = ParseOrderType(shippingMethod),
            };

            if (orderElem.TryGetProperty("updated_at", out var upEl) && upEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(upEl.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var upParsed))
                order.UpdatedAt = upParsed.ToLocalTime();

            return order;
        }

        private static bool TryGetJsonBoolean(JsonElement parent, params string[] propertyNames)
        {
            foreach (var name in propertyNames)
            {
                foreach (var prop in parent.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                    return prop.Value.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => prop.Value.GetInt32() != 0,
                        JsonValueKind.String => string.Equals(prop.Value.GetString(), "true", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(prop.Value.GetString(), "1", StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                }
            }

            return false;
        }

        private static string TryGetOrderStringInsensitive(JsonElement orderElem, params string[] candidateNames)
        {
            foreach (var c in candidateNames)
            {
                foreach (var prop in orderElem.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, c, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString()?.Trim() ?? "";
                    break;
                }
            }

            return "";
        }

        private static decimal TryGetOrderDecimalInsensitive(JsonElement orderElem, params string[] candidateNames)
        {
            foreach (var c in candidateNames)
            {
                foreach (var prop in orderElem.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, c, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.Number) return prop.Value.GetDecimal();
                    if (prop.Value.ValueKind == JsonValueKind.String && decimal.TryParse(prop.Value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                        return d;
                    return 0m;
                }
            }

            return 0m;
        }

        private static int TryGetOrderInt32(JsonElement orderElem, params string[] candidateNames)
        {
            foreach (var c in candidateNames)
            {
                foreach (var prop in orderElem.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, c, StringComparison.OrdinalIgnoreCase)) continue;
                    if (prop.Value.ValueKind == JsonValueKind.Number) return prop.Value.GetInt32();
                    if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        return i;
                    return 0;
                }
            }

            return 0;
        }

        /// <summary>Reads payment from tables <c>ongoing_orders</c>: prefers <c>payment_status</c> string; numeric status only forces PAID when non-zero (0 is not treated as definitive UNPAID). Then <c>is_paid</c> / <c>isPaid</c> only — avoids a numeric <c>paid</c> amount field being read as a boolean.</summary>
        private static string TryGetPaymentStatusFromOngoingOrder(JsonElement orderElem, out bool hasIsPaid, out bool isPaidFlag)
        {
            hasIsPaid = false;
            isPaidFlag = false;

            JsonElement? paymentStatusEl = null;
            foreach (var prop in orderElem.EnumerateObject())
            {
                if (string.Equals(prop.Name, "payment_status", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(prop.Name, "paymentStatus", StringComparison.OrdinalIgnoreCase))
                {
                    paymentStatusEl = prop.Value;
                    break;
                }
            }

            if (paymentStatusEl.HasValue)
            {
                var el = paymentStatusEl.Value;
                if (el.ValueKind == JsonValueKind.String)
                {
                    var s = el.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(s)) return s;
                }

                if (el.ValueKind == JsonValueKind.Number && el.GetInt32() != 0)
                    return "PAID";
            }

            foreach (var paidKey in new[] { "is_paid", "isPaid" })
            {
                foreach (var prop in orderElem.EnumerateObject())
                {
                    if (!string.Equals(prop.Name, paidKey, StringComparison.OrdinalIgnoreCase)) continue;
                    hasIsPaid = true;
                    var el = prop.Value;
                    isPaidFlag = el.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Number => el.GetInt32() != 0,
                        JsonValueKind.String => string.Equals(el.GetString(), "true", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(el.GetString(), "1", StringComparison.OrdinalIgnoreCase),
                        _ => false
                    };
                    return isPaidFlag ? "PAID" : "UNPAID";
                }
            }

            if (paymentStatusEl.HasValue && paymentStatusEl.Value.ValueKind == JsonValueKind.Number && paymentStatusEl.Value.GetInt32() == 0)
                return "UNPAID";

            return "";
        }

        private POS_UI.Models.TableStatus ParseTableStatus(string status)
        {
            return status?.ToUpper() switch
            {
                "AVAILABLE" => POS_UI.Models.TableStatus.Available,
                "RESERVED" => POS_UI.Models.TableStatus.Reserved,
                "DRAFTED" => POS_UI.Models.TableStatus.Drafted,
                "SERVED" => POS_UI.Models.TableStatus.Served,
                "UNAVAILABLE" => POS_UI.Models.TableStatus.Unavailable,
                _ => POS_UI.Models.TableStatus.Available
            };
        }

        private POS_UI.Models.OrderType ParseOrderType(string orderTypeString)
        {
            return orderTypeString?.ToUpper() switch
            {
                "DINE-IN" => POS_UI.Models.OrderType.DineIn,
                "TAKEAWAY" => POS_UI.Models.OrderType.TakeAway,
                "DELIVERY" => POS_UI.Models.OrderType.Delivery,
                "COLLECTION" => POS_UI.Models.OrderType.TakeAway,
                _ => POS_UI.Models.OrderType.DineIn
            };
        }


        public async Task<POS_UI.Models.OrderModel> GetOrderByIdAsync(int orderId)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to get order details.");
                }
                SetBearerToken(accessToken);

                var response = await _httpClient.GetAsync($"/api/v1/orders/{orderId}");
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                using var doc = JsonDocument.Parse(responseBody);
                var orderData = doc.RootElement.GetProperty("data");

                var order = new POS_UI.Models.OrderModel
                {
                    Id = Guid.NewGuid(),
                    ApiId = orderData.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0,
                    OrderNumber = orderData.TryGetProperty("display_order_id", out var orderIdProp) ? orderIdProp.GetString() ?? "Unknown" : "Unknown",
                    DisplayOrderId = orderData.TryGetProperty("display_order_id", out var displayIdProp) ? displayIdProp.GetString() ?? "" : "",
                    CustomerName = orderData.TryGetProperty("customer_name", out var customerProp) ? customerProp.GetString() ?? "" : "",
                    CustomerPhone = orderData.TryGetProperty("customer_phone", out var phoneProp) ? (phoneProp.GetString() ?? "") : "",
                    CustomerId = orderData.TryGetProperty("customer_id", out var customerIdProp) ? customerIdProp.GetInt32() : 0,
                    ApiTotal = orderData.TryGetProperty("total_amount", out var totalProp) ? totalProp.GetDecimal() : 0m,
                    ApiSubTotal = orderData.TryGetProperty("sub_total", out var subTotalProp) ? subTotalProp.GetDecimal() : 0m,
                    Status = ParseOrderStatus(orderData.TryGetProperty("status", out var statusProp) ? statusProp.GetString() ?? "" : ""),
                    ApiStatus = orderData.TryGetProperty("status", out var _statusProp2) ? _statusProp2.GetString() ?? string.Empty : string.Empty,
                    OrderType = ParseOrderType(orderData.TryGetProperty("shipping_method", out var shippingProp) ? shippingProp.GetString() ?? "" : ""),
                    TableNumber = orderData.TryGetProperty("table_id", out var tableProp) ? tableProp.GetInt32() : 0,
                    OrderNotes = orderData.TryGetProperty("note", out var noteProp) ? noteProp.GetString() ?? "" : 
                                (orderData.TryGetProperty("order_note", out var orderNoteProp) ? orderNoteProp.GetString() ?? "" : 
                                (orderData.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() ?? "" : "")),
                    CreatedAt = orderData.TryGetProperty("created_at", out var createdProp) ? DateTime.Parse(createdProp.GetString()).ToLocalTime() : DateTime.MinValue,
                    UpdatedAt = orderData.TryGetProperty("updated_at", out var updatedProp) ? DateTime.Parse(updatedProp.GetString()).ToLocalTime() : DateTime.MinValue,
                    PlatformId = orderData.TryGetProperty("delivery_platform_id", out var platformIdProp) ? platformIdProp.GetInt32() : 0,
                    PlatformName = orderData.TryGetProperty("platform_name", out var pnProp) ? pnProp.GetString() ?? "" : "",
                    DeliveryPlatfornName = orderData.TryGetProperty("delivery_platform_name", out var dpnProp) ? dpnProp.GetString() ?? "" : "",
                    DiscountModeApplied = orderData.TryGetProperty("discount_mode_applied", out var discountModeProp) ? discountModeProp.GetString() ?? "percentage" : "percentage",
                    // Parse discount value based on mode
                    DiscountAmount = orderData.TryGetProperty("discount", out var discountProp) ? discountProp.GetDecimal() : 0m,
                    DiscountPercentage = orderData.TryGetProperty("discount_percentage", out var discountPercentProp) ? discountPercentProp.GetDecimal() : 0m,
                    VoucherDiscount = orderData.TryGetProperty("voucher_discount", out var voucherDiscountProp) ? voucherDiscountProp.GetDecimal() : 0m,
                    DeliveryDateTime = orderData.TryGetProperty("delivery_date_time", out var orderDateTimeProp) ? DateTime.Parse(orderDateTimeProp.GetString()).ToLocalTime() : DateTime.MinValue,
                    PlatformLogo = orderData.TryGetProperty("platform_logo", out var platformLogoProp) ? platformLogoProp.GetString() ?? "" : "",
                    ShippingMethod = orderData.TryGetProperty("shipping_method", out var shippingMethodProp) ? shippingMethodProp.GetString() ?? "" : "",
                    PaymentStatus = orderData.TryGetProperty("payment_status", out var paymentStatusProp) ? paymentStatusProp.GetString() ?? "" : "",
                    PaymentMethod = orderData.TryGetProperty("payment_method", out var paymentMethodProp) ? paymentMethodProp.GetString() ?? "" : "",
                    PlatformId2 = orderData.TryGetProperty("platform_id", out var platformId2Prop) ? platformId2Prop.GetInt32() : 0,
                    RemoteOrderId = orderData.TryGetProperty("remote_order_id", out var remoteOrderIdProp) ? remoteOrderIdProp.GetString() ?? null : null,
                    TableName = orderData.TryGetProperty("table_name", out var tableNameProp) ? tableNameProp.GetString() ?? null : null,
                    OrderDelayed = orderData.TryGetProperty("order_delayed", out var orderDelayedProp) ? orderDelayedProp.GetBoolean() : false,
                    PaymentMode = orderData.TryGetProperty("payment_mode", out var paymentModeProp) ? paymentModeProp.GetString() ?? "" : "",
                    TableOrderMethod = orderData.TryGetProperty("table_order_method", out var tableOrderMethodProp) ? tableOrderMethodProp.GetString() ?? "" : "",
                    OrderSessionId = orderData.TryGetProperty("order_session_id", out var orderSessionIdProp) ? orderSessionIdProp.GetInt32() : 0,
                    TableOrderingsId = orderData.TryGetProperty("table_orderings_id", out var tableOrderingsIdProp) ? tableOrderingsIdProp.GetInt32() : 0,
                    RefundBalance = orderData.TryGetProperty("refund_balance", out var refundBalanceProp) ? refundBalanceProp.GetDecimal() : 0m,
                    PaymentType = orderData.TryGetProperty("payment_type", out var paymentTypeProp) ? paymentTypeProp.GetString() ?? "" : "",
                    SessionPaymentType = orderData.TryGetProperty("session_payment_type", out var sessionPaymentTypeProp) ? sessionPaymentTypeProp.GetString() ?? "" : "",
                    RefundStatus = orderData.TryGetProperty("refund_status", out var refundStatusProp) ? refundStatusProp.GetString() ?? "" : "",
                };

                if (orderData.TryGetProperty("refund_balances", out var refundBalancesProp) && refundBalancesProp.ValueKind == JsonValueKind.Object)
                {
                    order.RefundBalances = new POS_UI.Models.RefundBalancesModel
                    {
                        TotalRefundBalance = TryGetDecimal(refundBalancesProp, "total_refund_balance"),
                        CashRefundBalance = TryGetDecimal(refundBalancesProp, "cash_refund_balance"),
                        CardRefundBalance = TryGetDecimal(refundBalancesProp, "card_refund_balance")
                    };
                    order.RefundBalance = order.RefundBalances.TotalRefundBalance;
                }

                // Delivery charge (webshop orders and others)
                if (orderData.TryGetProperty("shipping_total", out var shippingTotal))
                {
                    if (shippingTotal.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(shippingTotal.GetString().Replace(" ", string.Empty), out var shippingVal))
                        {
                            order.DeliveryCharge = shippingVal;
                        }
                    }
                    else if (shippingTotal.ValueKind == JsonValueKind.Number)
                    {
                        order.DeliveryCharge = shippingTotal.GetDecimal();
                    }
                }
                else if (orderData.TryGetProperty("shipping_total_amount", out var shippingTotalAmt))
                {
                    if (shippingTotalAmt.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(shippingTotalAmt.GetString().Replace(" ", string.Empty), out var shippingVal2))
                        {
                            order.DeliveryCharge = shippingVal2;
                        }
                    }
                    else if (shippingTotalAmt.ValueKind == JsonValueKind.Number)
                    {
                        order.DeliveryCharge = shippingTotalAmt.GetDecimal();
                    }
                }

                // Extract delivery tax information if available (from root delivery properties)
                if (orderData.TryGetProperty("shipping_tax", out var shippingTaxProp) && 
                    orderData.TryGetProperty("delivery_tax", out var deliveryTaxProp))
                {
                    decimal shipTaxAmt = 0m;
                    if (deliveryTaxProp.ValueKind == JsonValueKind.Number) shipTaxAmt = deliveryTaxProp.GetDecimal();
                    else if (deliveryTaxProp.ValueKind == JsonValueKind.String && decimal.TryParse(deliveryTaxProp.GetString(), out var parsed)) shipTaxAmt = parsed;

                    // Only populate if there's a tax amount or explicit tax details
                    if (shipTaxAmt > 0)
                    {
                        var deliveryTaxId = TryGetNullableInt(orderData, "tax_id");
                        var deliveryTaxRate = TryGetDecimal(orderData, "tax_rate");
                        var deliveryTaxCode = orderData.TryGetProperty("tax_code", out var tcProp) ? tcProp.GetString() : null;

                        order.DeliveryTaxDetail = new POS_UI.Models.TaxDetailModel
                        {
                            TaxId = deliveryTaxId,
                            TaxCode = deliveryTaxCode,
                            Rate = deliveryTaxRate,
                            Amount = Math.Round(shipTaxAmt, 2, MidpointRounding.AwayFromZero),
                            // Taxable amount is the delivery charge itself
                            TaxableAmount = order.DeliveryCharge
                        };
                        order.ShippingTaxAmount = shipTaxAmt;
                    }
                }

                if (orderData.TryGetProperty("shipping_details", out var sdProp) && sdProp.ValueKind == JsonValueKind.Object)
                {
                    string deliveryAddressLine1 = sdProp.TryGetProperty("address_line_1", out var al1) && al1.ValueKind != JsonValueKind.Null ? al1.GetString() : null;
                    string deliveryAddressLine2 = sdProp.TryGetProperty("address_line_2", out var al2) && al2.ValueKind != JsonValueKind.Null ? al2.GetString() : null;
                    string house = sdProp.TryGetProperty("house_no", out var hn) && hn.ValueKind != JsonValueKind.Null ? hn.GetString() : null;
                    string cityVal = sdProp.TryGetProperty("city", out var cityProp) && cityProp.ValueKind != JsonValueKind.Null ? cityProp.GetString() : null;
                    string postcodeVal = sdProp.TryGetProperty("postcode", out var postcodeProp) && postcodeProp.ValueKind != JsonValueKind.Null ? postcodeProp.GetString() : null;
                    string flatNo = sdProp.TryGetProperty("flat_no", out var flatNoProp) && flatNoProp.ValueKind != JsonValueKind.Null ? flatNoProp.GetString() : null;
                    order.DeliveryAddressLine1 = deliveryAddressLine1;
                    order.DeliveryAddressLine2 = deliveryAddressLine2;
                    order.DeliveryCity = cityVal;
                    order.DeliveryFlatNo = flatNo;
                    order.DeliveryPostcode = postcodeVal;
                    // Build display according to requirement: address_line_1 and address_line_2 (optionally prefix house no)
                    if (!string.IsNullOrWhiteSpace(deliveryAddressLine1) || !string.IsNullOrWhiteSpace(deliveryAddressLine2))
                    {
                        var firstSegments = new[] { flatNo, house, deliveryAddressLine1 }.Where(s => !string.IsNullOrWhiteSpace(s));
                        var first = string.Join(" ", firstSegments).Trim();
                        var parts = new List<string>();
                        if (!string.IsNullOrWhiteSpace(first)) parts.Add(first);
                        if (!string.IsNullOrWhiteSpace(deliveryAddressLine2)) parts.Add(deliveryAddressLine2);
                        if (!string.IsNullOrWhiteSpace(cityVal)) parts.Add(cityVal);
                        if (!string.IsNullOrWhiteSpace(postcodeVal)) parts.Add(postcodeVal);
                        order.DeliveryAddress = string.Join(", ", parts);
                    }
                }

                // Loyalty / reward discount (divide by 100 if provided in cents)
                if (orderData.TryGetProperty("loyalty", out var loyaltyObj) && loyaltyObj.ValueKind == JsonValueKind.Object)
                {
                    if (loyaltyObj.TryGetProperty("redeemed_amount", out var redeemed))
                    {
                        if (redeemed.ValueKind == JsonValueKind.String)
                        {
                            if (decimal.TryParse(redeemed.GetString().Replace(" ", string.Empty), out var raw))
                            {
                                order.RewardDiscount = raw / 100m;
                            }
                        }
                        else if (redeemed.ValueKind == JsonValueKind.Number)
                        {
                            order.RewardDiscount = redeemed.GetDecimal() / 100m;
                        }
                    }
                }

                // Set Platform name based on PlatformId
                /*order.Platform = order.PlatformId switch
                {
                    1 => "Deliveroo",
                    2 => "UberEats",
                    6 => "Webshop",
                    8 => "Table Order",
                    9 => "DG POS",
                    _ => "DG POS" // Default fallback
                };*/

                // Backfill customer phone from delivergate_customer if not provided in root
                if (string.IsNullOrWhiteSpace(order.CustomerPhone) && orderData.TryGetProperty("delivergate_customer", out var dgc) && dgc.ValueKind == JsonValueKind.Object)
                {
                    string cc = dgc.TryGetProperty("country_code", out var ccProp) && ccProp.ValueKind == JsonValueKind.String ? ccProp.GetString() : null;
                    string ph = dgc.TryGetProperty("phone", out var phProp) && phProp.ValueKind == JsonValueKind.String ? phProp.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(ph))
                    {
                        order.CustomerPhone = string.IsNullOrWhiteSpace(cc) ? ph : ($"{cc}{(ph.StartsWith("+") ? "" : "")}{ph}");
                    }
                }

                // Parse order items if they exist
                if (orderData.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    var items = new List<POS_UI.Models.OrderItem>();
                    foreach (var itemElem in itemsProp.EnumerateArray())
                    {
                        var item = ParseOrderItem(itemElem);
                        
                        items.Add(item);
                    }
                    order.Items = items;
                }

                // Parse vouchers if they exist
                if (orderData.TryGetProperty("vouchers", out var vouchersProp) && vouchersProp.ValueKind == JsonValueKind.Array)
                {
                    var vouchers = new List<POS_UI.Models.VoucherModel>();
                    foreach (var voucherElem in vouchersProp.EnumerateArray())
                    {
                        var voucherCode = voucherElem.TryGetProperty("voucher_code", out var vcProp) ? vcProp.GetString() ?? "" : "";
                        var voucherValue = voucherElem.TryGetProperty("voucher_value", out var vvProp) ? vvProp.GetString() ?? "" : "";
                        var valueType = voucherElem.TryGetProperty("value_type", out var vtProp) ? vtProp.GetString() ?? "" : "";
                        var voucherDiscount = 0m;
                        
                        if (voucherElem.TryGetProperty("voucher_discount", out var vdProp))
                        {
                            if (vdProp.ValueKind == JsonValueKind.Number)
                            {
                                voucherDiscount = vdProp.GetDecimal();
                            }
                            else if (vdProp.ValueKind == JsonValueKind.String && decimal.TryParse(vdProp.GetString(), out var parsedDiscount))
                            {
                                voucherDiscount = parsedDiscount;
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(voucherCode) || voucherDiscount > 0m)
                        {
                            vouchers.Add(new POS_UI.Models.VoucherModel
                            {
                                VoucherCode = voucherCode,
                                VoucherValue = voucherValue,
                                ValueType = valueType,
                                VoucherDiscount = voucherDiscount
                            });
                        }
                    }
                    
                    if (vouchers.Count > 0)
                    {
                        order.Vouchers = vouchers;
                    }
                }

                // Parse shop_fees if they exist
                if (orderData.TryGetProperty("shop_fees", out var shopFeesProp) && shopFeesProp.ValueKind == JsonValueKind.Array)
                {
                    var orderShopFees = new List<POS_UI.Models.OrderShopFeeModel>();
                    foreach (var feeElem in shopFeesProp.EnumerateArray())
                    {
                        try
                        {
                            var feeAmountRaw = 0m;
                            if (feeElem.TryGetProperty("amount", out var amountProp))
                            {
                                if (amountProp.ValueKind == JsonValueKind.String)
                                {
                                    decimal.TryParse(amountProp.GetString().Replace(" ", ""), out feeAmountRaw);
                                }
                                else if (amountProp.ValueKind == JsonValueKind.Number)
                                {
                                    feeAmountRaw = amountProp.GetDecimal();
                                }
                            }
                            var feeAmount = Math.Round(feeAmountRaw, 2, MidpointRounding.AwayFromZero);
                            
                            if (feeAmount > 0)
                            {
                                var shopFeeId = feeElem.TryGetProperty("shop_fee_id", out var shopFeeIdProp) ? shopFeeIdProp.GetInt32() : 0;
                                var feeName = feeElem.TryGetProperty("fee_name", out var nameProp) ? nameProp.GetString() : "Fee";
                                var feeType = feeElem.TryGetProperty("fee_type", out var typeProp) ? typeProp.GetString() : null;
                                // Fee percentage/value as numeric from API may be string
                                decimal feeValue = 0m;
                                if (feeElem.TryGetProperty("fee", out var feeValProp))
                                {
                                    if (feeValProp.ValueKind == JsonValueKind.String)
                                    {
                                        decimal.TryParse(feeValProp.GetString(), out feeValue);
                                    }
                                    else if (feeValProp.ValueKind == JsonValueKind.Number)
                                    {
                                        feeValue = feeValProp.GetDecimal();
                                    }
                                }
                                if (feeValue <= 0m && feeElem.TryGetProperty("fee_value", out var feeValueProp))
                                {
                                    if (feeValueProp.ValueKind == JsonValueKind.String)
                                    {
                                        decimal.TryParse(feeValueProp.GetString(), out feeValue);
                                    }
                                    else if (feeValueProp.ValueKind == JsonValueKind.Number)
                                    {
                                        feeValue = feeValueProp.GetDecimal();
                                    }
                                }
                                if (feeValue <= 0m && feeElem.TryGetProperty("value", out var genericValueProp))
                                {
                                    if (genericValueProp.ValueKind == JsonValueKind.String)
                                    {
                                        decimal.TryParse(genericValueProp.GetString(), out feeValue);
                                    }
                                    else if (genericValueProp.ValueKind == JsonValueKind.Number)
                                    {
                                        feeValue = genericValueProp.GetDecimal();
                                    }
                                }

                                var feeTaxAmount = 0m;
                                if (feeElem.TryGetProperty("shop_fee_tax", out var feeTaxProp))
                                {
                                    feeTaxAmount = TryGetDecimal(feeTaxProp);
                                }
                                else if (feeElem.TryGetProperty("tax_amount", out var feeTaxAmountProp))
                                {
                                    feeTaxAmount = TryGetDecimal(feeTaxAmountProp);
                                }

                                var feeTaxId = TryGetNullableInt(feeElem, "tax_id");
                                var feeTaxProfileId = TryGetNullableInt(feeElem, "tax_profile_id");
                                var feeTaxRate = TryGetDecimal(feeElem, "tax_rate");
                                var feeTaxCode = feeElem.TryGetProperty("tax_code", out var taxCodeProp) ? taxCodeProp.GetString() : null;
                                
                                orderShopFees.Add(new POS_UI.Models.OrderShopFeeModel
                                {
                                    ShopFeeId = shopFeeId,
                                    Type = feeElem.TryGetProperty("type", out var feeTypeProp2) ? feeTypeProp2.GetString() : null,
                                    Name = feeName,
                                    Amount = feeAmount,
                                    FeeType = feeType,
                                    FeeValue = feeValue,
                                    IsMandatory = feeElem.TryGetProperty("mandatory", out var mandatoryProp) && mandatoryProp.ValueKind == JsonValueKind.True,
                                    TaxAmount = Math.Round(Math.Max(0m, feeTaxAmount), 2, MidpointRounding.AwayFromZero),
                                    TaxId = feeTaxId,
                                    TaxProfileId = feeTaxProfileId,
                                    TaxCode = feeTaxCode,
                                    TaxRate = feeTaxRate
                                });
                            }
                        }
                        catch { /* ignore malformed fee item */ }
                    }
                    order.OrderShopFees = orderShopFees;
                }

                // Parse order_taxes if they exist
                if (orderData.TryGetProperty("order_taxes", out var orderTaxesProp) && orderTaxesProp.ValueKind == JsonValueKind.Array)
                {
                    var orderTaxes = new List<POS_UI.Models.TaxSummaryRow>();
                    foreach (var taxElem in orderTaxesProp.EnumerateArray())
                    {
                        try
                        {
                            var taxRate = 0m;
                            if (taxElem.TryGetProperty("tax_rate", out var taxRateProp))
                            {
                                if (taxRateProp.ValueKind == JsonValueKind.String)
                                {
                                    decimal.TryParse(taxRateProp.GetString().Replace(" ", ""), out taxRate);
                                }
                                else if (taxRateProp.ValueKind == JsonValueKind.Number)
                                {
                                    taxRate = taxRateProp.GetDecimal();
                                }
                            }

                            var taxAmount = 0m;
                            if (taxElem.TryGetProperty("tax_amount", out var taxAmountProp))
                            {
                                if (taxAmountProp.ValueKind == JsonValueKind.String)
                                {
                                    decimal.TryParse(taxAmountProp.GetString().Replace(" ", ""), out taxAmount);
                                }
                                else if (taxAmountProp.ValueKind == JsonValueKind.Number)
                                {
                                    taxAmount = taxAmountProp.GetDecimal();
                                }
                            }

                            var taxableAmount = 0m;
                            if (taxElem.TryGetProperty("taxable_amount", out var taxableAmountProp))
                            {
                                if (taxableAmountProp.ValueKind == JsonValueKind.String)
                                {
                                    decimal.TryParse(taxableAmountProp.GetString().Replace(" ", ""), out taxableAmount);
                                }
                                else if (taxableAmountProp.ValueKind == JsonValueKind.Number)
                                {
                                    taxableAmount = taxableAmountProp.GetDecimal();
                                }
                            }

                            var taxCode = taxElem.TryGetProperty("tax_code", out var taxCodeProp) ? taxCodeProp.GetString() ?? "" : "";

                            orderTaxes.Add(new POS_UI.Models.TaxSummaryRow
                            {
                                Rate = taxRate,
                                TaxCode = taxCode,
                                TaxAmount = Math.Round(taxAmount, 2, MidpointRounding.AwayFromZero),
                                TaxableAmount = Math.Round(taxableAmount, 2, MidpointRounding.AwayFromZero)
                            });
                        }
                        catch { /* ignore malformed tax item */ }
                    }
                    order.TaxSummaryRows = orderTaxes;
                }

                // Parse transactions if they exist (refunds, etc.)
                if (orderData.TryGetProperty("transactions", out var transactionsProp) && transactionsProp.ValueKind == JsonValueKind.Array)
                {
                    var transactions = new List<POS_UI.Models.OrderTransactionModel>();
                    foreach (var transElem in transactionsProp.EnumerateArray())
                    {
                        try
                        {
                            var transactionType = transElem.TryGetProperty("transaction_type", out var transTypeProp) ? transTypeProp.GetString() ?? "" : "";
                            
                            var transactionAmount = 0m;
                            if (transElem.TryGetProperty("transaction_amount", out var transAmountProp))
                            {
                                if (transAmountProp.ValueKind == JsonValueKind.String)
                                {
                                    decimal.TryParse(transAmountProp.GetString().Replace(" ", ""), out transactionAmount);
                                }
                                else if (transAmountProp.ValueKind == JsonValueKind.Number)
                                {
                                    transactionAmount = transAmountProp.GetDecimal();
                                }
                            }

                            var transactionMode = transElem.TryGetProperty("transaction_mode", out var transModeProp) ? transModeProp.GetString() ?? "" : "";
                            var reason = transElem.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() ?? "" : "";

                            DateTime createdAt = DateTime.MinValue;
                            if (transElem.TryGetProperty("created_at", out var transCreatedProp) && transCreatedProp.ValueKind == JsonValueKind.String)
                            {
                                var createdStr = transCreatedProp.GetString();
                                if (!string.IsNullOrEmpty(createdStr))
                                {
                                    DateTime.TryParse(createdStr, out createdAt);
                                    createdAt = createdAt.ToLocalTime();
                                }
                            }

                            DateTime updatedAt = DateTime.MinValue;
                            if (transElem.TryGetProperty("updated_at", out var transUpdatedProp) && transUpdatedProp.ValueKind == JsonValueKind.String)
                            {
                                var updatedStr = transUpdatedProp.GetString();
                                if (!string.IsNullOrEmpty(updatedStr))
                                {
                                    DateTime.TryParse(updatedStr, out updatedAt);
                                    updatedAt = updatedAt.ToLocalTime();
                                }
                            }

                            transactions.Add(new POS_UI.Models.OrderTransactionModel
                            {
                                TransactionType = transactionType,
                                TransactionAmount = transactionAmount,
                                TransactionMode = transactionMode,
                                CreatedAt = createdAt,
                                UpdatedAt = updatedAt,
                                Reason = reason
                            });
                        }
                        catch { /* ignore malformed transaction item */ }
                    }
                    order.Transactions = transactions;
                }

                return order;
            }
            catch (Exception ex)
            {
                // Check if it's a network connectivity issue
                var networkService = POS_UI.Services.NetworkConnectivityService.Instance;
                bool isNetworkError = !networkService.IsConnected || 
                                    ex.Message.Contains("Unable to connect") ||
                                    ex.Message.Contains("No such host") ||
                                    ex.Message.Contains("Connection refused") ||
                                    ex.Message.Contains("Network is unreachable") ||
                                    ex.Message.Contains("Timeout") ||
                                    ex.Message.Contains("The remote name could not be resolved") ||
                                    ex.Message.Contains("A connection attempt failed") ||
                                    ex.Message.Contains("The operation has timed out");
                
                if (isNetworkError)
                {
                    // Don't show error message if there's no internet connection
                    // The internet connection dialog will handle this
                    throw;
                }
                
                //MessageBox.Show($"Error getting order details: {ex.Message}");
                return null;
            }
        }

        public async Task<POS_UI.Models.CurrentUserModel> GetCurrentUserAsync()
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var response = await _httpClient.GetAsync("/api/v1/users/current");
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                return new POS_UI.Models.CurrentUserModel
                {
                    Id = data.GetProperty("id").GetInt32(),
                    FirstName = data.GetProperty("first_name").GetString(),
                    LastName = data.GetProperty("last_name").GetString(),
                    Email = data.GetProperty("email").GetString(),
                    Address = data.GetProperty("address").GetString(),
                    ContactNo = data.GetProperty("contact_no").GetString(),
                    Status = data.GetProperty("status").GetString(),
                    RoleId = data.GetProperty("role_id").GetString(),
                    Role = data.GetProperty("role").GetString(),
                    ReportServiceToken = data.TryGetProperty("report_service_token", out var reportTokenEl) ? reportTokenEl.GetString() : null,
                    CreatedAt = DateTime.Parse(data.GetProperty("created_at").GetString()),
                    UpdatedAt = DateTime.Parse(data.GetProperty("updated_at").GetString())
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get current user: {ex.Message}");
            }
        }

        public async Task<POS_UI.Models.ShopModel> GetShopDetailsAsync(string code, string brandId)
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                var url = $"/api/v1/shop-info?code={code}&brandId={brandId}";
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetShopDetails"); } catch { }
                    }
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                var shop = new POS_UI.Models.ShopModel
                {
                    Id = data.GetProperty("id").GetInt32(),
                    Name = data.GetProperty("name").GetString(),
                    ShopLogo = data.TryGetProperty("shop_logo", out var shopLogoEl) ? shopLogoEl.GetString() : null,
                    FranchiseId = data.GetProperty("franchise_id").GetInt32(),
                    Code = data.GetProperty("code").GetString(),
                    Email = data.GetProperty("email").GetString(),
                    Address = data.GetProperty("address").GetString(),
                    ContactNo = data.GetProperty("contact_no").GetString(),
                    BusinessRegNo = data.GetProperty("business_reg_no").GetString(),
                    Status = data.GetProperty("status").GetString(),
                    OrderStatus = data.GetProperty("order_status").GetString(),
                    LastUpdatedMenu = data.GetProperty("last_updated_menu").GetInt32(),
                    ServiceAvailability = data.GetProperty("service_availability").GetString(),
                    MinimumAmountForFreeDelivery = data.GetProperty("minimum_amount_for_free_delivery").GetDecimal(),
                    MinimumAmountForDelivery = data.GetProperty("minimum_amount_for_delivery").GetDecimal(),
                    OneTimePromotionValue = data.GetProperty("one_time_promotion_value").GetDecimal(),
                    OneTimePromotionSpendAmount = data.GetProperty("one_time_promotion_spend_amount").GetDecimal(),
                    Latitude = data.GetProperty("latitude").GetString(),
                    Longitude = data.GetProperty("longitude").GetString(),
                    GoogleLocationUrl = data.GetProperty("google_location_url").GetString(),
                    OneTimePromotionType = data.GetProperty("one_time_promotion_type").GetString(),
                    MaximumPromotionValue = data.GetProperty("maximum_promotion_value").GetDecimal(),
                    SelectedMenu = data.GetProperty("selected_menu").GetInt32(),
                    IsDefault = data.GetProperty("is_default").GetBoolean(),
                    CountryCode = data.GetProperty("country_code").GetString(),
                    Timezone = data.GetProperty("timezone").GetString(),
                    Currency = data.GetProperty("currency").GetString(),
                    CurrencyCode = data.GetProperty("currency_code").GetString(),
                    DeliveryPlatformEnable = data.GetProperty("delivery_platform_enable").GetBoolean(),
                    HasCashPayment = data.GetProperty("has_cash_payment").GetBoolean(),
                    HasCardPayment = data.GetProperty("has_card_payment").GetBoolean(),
                    DelivergateAccount = data.GetProperty("delivergate_account").GetBoolean(),
                    CreatedAt = DateTime.Parse(data.GetProperty("created_at").GetString()),
                    UpdatedAt = DateTime.Parse(data.GetProperty("updated_at").GetString()),
                    TaxRegNo = data.GetProperty("tax_reg_no").GetString()
                };

                var taxModeValue = "none";
                if (data.TryGetProperty("tax_mode", out var taxModeProp) && taxModeProp.ValueKind == JsonValueKind.String)
                {
                    var rawMode = taxModeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(rawMode))
                    {
                        taxModeValue = rawMode.Trim();
                    }
                }
                else if (data.TryGetProperty("tax_inclusive", out var legacyTaxInclusiveProp))
                {
                    // Legacy fallback: infer tax mode when only the old field exists
                    if (legacyTaxInclusiveProp.ValueKind == JsonValueKind.String)
                    {
                        var isInclusive = string.Equals(legacyTaxInclusiveProp.GetString(), "true", StringComparison.OrdinalIgnoreCase);
                        taxModeValue = isInclusive ? "inclusive" : "exclusive";
                    }
                    else if (legacyTaxInclusiveProp.ValueKind == JsonValueKind.True || legacyTaxInclusiveProp.ValueKind == JsonValueKind.False)
                    {
                        taxModeValue = legacyTaxInclusiveProp.GetBoolean() ? "inclusive" : "exclusive";
                    }
                }

                shop.TaxMode = string.IsNullOrWhiteSpace(taxModeValue) ? "none" : taxModeValue;
                shop.TaxInclusive = string.Equals(shop.TaxMode, "inclusive", StringComparison.OrdinalIgnoreCase);

                // Parse delivery platform if it exists
                if (data.TryGetProperty("delivery_platform", out var deliveryPlatformElement))
                {
                    var deliveryPlatform = new POS_UI.Models.DeliveryPlatformModel
                    {
                        Id = deliveryPlatformElement.GetProperty("id").GetInt32(),
                        PlatformId = deliveryPlatformElement.GetProperty("platform_id").GetInt32(),
                        Name = deliveryPlatformElement.GetProperty("name").GetString(),
                        Logo = deliveryPlatformElement.GetProperty("logo").GetString(),
                        Status = deliveryPlatformElement.GetProperty("status").GetString(),
                        OutletCode = deliveryPlatformElement.GetProperty("outlet_code").GetString(),
                        FranchiseId = deliveryPlatformElement.GetProperty("franchise_id").GetInt32(),
                        OutletId = deliveryPlatformElement.GetProperty("outlet_id").GetInt32(),
                        StoreStatus = deliveryPlatformElement.GetProperty("store_status").GetString(),
                        AvailableFrom = DateTime.Parse(deliveryPlatformElement.GetProperty("available_from").GetString()),
                        IsMaster = deliveryPlatformElement.GetProperty("is_master").GetBoolean(),
                        ParentPlatform = deliveryPlatformElement.GetProperty("parent_platform").GetInt32(),
                        PrepTime = deliveryPlatformElement.GetProperty("prep_time").GetInt32(),
                        OwnDriver = deliveryPlatformElement.GetProperty("own_driver").GetBoolean(),
                        MenuPublishedAt = DateTime.Parse(deliveryPlatformElement.GetProperty("menu_published_at").GetString()),
                        WebshopSetupStatus = deliveryPlatformElement.GetProperty("webshop_setup_status").GetBoolean(),
                        HasCashPayment = deliveryPlatformElement.GetProperty("has_cash_payment").GetBoolean(),
                        HasCardPayment = deliveryPlatformElement.GetProperty("has_card_payment").GetBoolean(),
                        SelectedMenu = deliveryPlatformElement.GetProperty("selected_menu").GetInt32(),
                        CreatedAt = DateTime.Parse(deliveryPlatformElement.GetProperty("created_at").GetString()),
                        UpdatedAt = DateTime.Parse(deliveryPlatformElement.GetProperty("updated_at").GetString()),
                        BrandName = deliveryPlatformElement.GetProperty("webshop_brand_name").GetString()
                    };
                    shop.DeliveryPlatform = deliveryPlatform;
                }

                // Parse shop_fees if available
                if (data.TryGetProperty("shop_fees", out var shopFeesElem) && shopFeesElem.ValueKind == JsonValueKind.Array)
                {
                    var fees = new List<POS_UI.Models.ShopFeeModel>();
                    foreach (var feeElem in shopFeesElem.EnumerateArray())
                    {
                        try
                        {
                            var feeModel = new POS_UI.Models.ShopFeeModel
                            {
                                // id may not exist in response; guard with TryGetProperty
                                Id = feeElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0,
                                Type = feeElem.TryGetProperty("type", out var tProp) ? tProp.GetString() : null,
                                FeeType = feeElem.TryGetProperty("fee_type", out var ftProp) ? ftProp.GetString() : null,
                                FeeName = feeElem.TryGetProperty("fee_name", out var fnProp) ? fnProp.GetString() : null,
                                Fee = feeElem.TryGetProperty("fee", out var fProp) && fProp.ValueKind == JsonValueKind.Number ? fProp.GetDecimal() : 0m,
                                Mandatory = feeElem.TryGetProperty("mandatory", out var mProp) && mProp.ValueKind == JsonValueKind.True
                            };

                            if (feeElem.TryGetProperty("tax_details", out var taxDetails) && taxDetails.ValueKind == JsonValueKind.Object)
                            {
                                feeModel.TaxId = taxDetails.TryGetProperty("tax_id", out var taxIdProp) && taxIdProp.ValueKind == JsonValueKind.Number
                                    ? taxIdProp.GetInt32()
                                    : (int?)null;
                                feeModel.TaxProfileId = taxDetails.TryGetProperty("tax_profile_id", out var taxProfileProp) && taxProfileProp.ValueKind == JsonValueKind.Number
                                    ? taxProfileProp.GetInt32()
                                    : (int?)null;
                                feeModel.TaxCode = taxDetails.TryGetProperty("tax_code", out var taxCodeProp) ? taxCodeProp.GetString() : null;
                                feeModel.TaxRate = taxDetails.TryGetProperty("tax_rate", out var taxRateProp) && taxRateProp.ValueKind == JsonValueKind.Number
                                    ? taxRateProp.GetDecimal()
                                    : 0m;
                            }
                            fees.Add(feeModel);
                        }
                        catch { /* ignore malformed fee item */ }
                    }
                    shop.ShopFees = fees;
                }

                if (data.TryGetProperty("taxes", out var taxesElem) && taxesElem.ValueKind == JsonValueKind.Array)
                {
                    var taxes = new List<POS_UI.Models.TaxModel>();
                    foreach (var taxElem in taxesElem.EnumerateArray())
                    {
                        try
                        {
                            taxes.Add(new POS_UI.Models.TaxModel
                            {
                                Id = taxElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0,
                                Name = taxElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                                Code = taxElem.TryGetProperty("code", out var codeProp) ? codeProp.GetString() : null,
                                Description = taxElem.TryGetProperty("description", out var descProp) ? descProp.GetString() : null,
                                Rate = taxElem.TryGetProperty("rate", out var rateProp) && rateProp.ValueKind == JsonValueKind.Number ? rateProp.GetDecimal() : 0m,
                                Status = taxElem.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.True ? true : statusProp.ValueKind == JsonValueKind.False ? false : true,
                                CreatedAt = taxElem.TryGetProperty("created_at", out var createdProp) && createdProp.ValueKind == JsonValueKind.String ? DateTime.Parse(createdProp.GetString()) : DateTime.MinValue,
                                UpdatedAt = taxElem.TryGetProperty("updated_at", out var updatedProp) && updatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(updatedProp.GetString()) : DateTime.MinValue
                            });
                        }
                        catch
                        {
                            // Ignore malformed tax entries
                        }
                    }

                    shop.Taxes = taxes;
                }

                if (data.TryGetProperty("tax_profiles", out var taxProfilesElem) && taxProfilesElem.ValueKind == JsonValueKind.Array)
                {
                    var profiles = new List<POS_UI.Models.TaxProfileModel>();

                    foreach (var profileElem in taxProfilesElem.EnumerateArray())
                    {
                        try
                        {
                            var profile = new POS_UI.Models.TaxProfileModel
                            {
                                Id = profileElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0,
                                Name = profileElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                                Description = profileElem.TryGetProperty("description", out var descProp) ? descProp.GetString() : null,
                                Status = profileElem.TryGetProperty("status", out var statusProp) && statusProp.ValueKind == JsonValueKind.True ? true : statusProp.ValueKind == JsonValueKind.False ? false : true,
                                CreatedAt = profileElem.TryGetProperty("created_at", out var createdProp) && createdProp.ValueKind == JsonValueKind.String ? DateTime.Parse(createdProp.GetString()) : DateTime.MinValue,
                                UpdatedAt = profileElem.TryGetProperty("updated_at", out var updatedProp) && updatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(updatedProp.GetString()) : DateTime.MinValue
                            };

                            if (profileElem.TryGetProperty("tax_rules", out var rulesElem) && rulesElem.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var ruleElem in rulesElem.EnumerateArray())
                                {
                                    try
                                    {
                                        var rule = new POS_UI.Models.TaxRuleModel
                                        {
                                            Id = ruleElem.TryGetProperty("id", out var ruleIdProp) && ruleIdProp.ValueKind == JsonValueKind.Number ? ruleIdProp.GetInt32() : 0,
                                            TaxProfileId = ruleElem.TryGetProperty("tax_profile_id", out var profileIdProp) && profileIdProp.ValueKind == JsonValueKind.Number ? profileIdProp.GetInt32() : profile.Id,
                                            TaxId = ruleElem.TryGetProperty("tax_id", out var taxIdProp) && taxIdProp.ValueKind == JsonValueKind.Number ? taxIdProp.GetInt32() : 0,
                                            Name = ruleElem.TryGetProperty("name", out var ruleNameProp) ? ruleNameProp.GetString() : null,
                                            CreatedAt = ruleElem.TryGetProperty("created_at", out var ruleCreatedProp) && ruleCreatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(ruleCreatedProp.GetString()) : DateTime.MinValue,
                                            UpdatedAt = ruleElem.TryGetProperty("updated_at", out var ruleUpdatedProp) && ruleUpdatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(ruleUpdatedProp.GetString()) : DateTime.MinValue
                                        };

                                        if (ruleElem.TryGetProperty("tax", out var taxElem) && taxElem.ValueKind == JsonValueKind.Object)
                                        {
                                            rule.Tax = new POS_UI.Models.TaxModel
                                            {
                                                Id = taxElem.TryGetProperty("id", out var taxId) && taxId.ValueKind == JsonValueKind.Number ? taxId.GetInt32() : rule.TaxId,
                                                Name = taxElem.TryGetProperty("name", out var taxNameProp) ? taxNameProp.GetString() : null,
                                                Code = taxElem.TryGetProperty("code", out var taxCodeProp) ? taxCodeProp.GetString() : null,
                                                Description = taxElem.TryGetProperty("description", out var taxDescProp) ? taxDescProp.GetString() : null,
                                                Rate = taxElem.TryGetProperty("rate", out var taxRateProp) && taxRateProp.ValueKind == JsonValueKind.Number ? taxRateProp.GetDecimal() : 0m,
                                                Status = taxElem.TryGetProperty("status", out var taxStatusProp) && taxStatusProp.ValueKind == JsonValueKind.True ? true : taxStatusProp.ValueKind == JsonValueKind.False ? false : true,
                                                CreatedAt = taxElem.TryGetProperty("created_at", out var taxCreatedProp) && taxCreatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(taxCreatedProp.GetString()) : DateTime.MinValue,
                                                UpdatedAt = taxElem.TryGetProperty("updated_at", out var taxUpdatedProp) && taxUpdatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(taxUpdatedProp.GetString()) : DateTime.MinValue
                                            };
                                        }

                                        if (ruleElem.TryGetProperty("tax_rule_conditions", out var conditionsElem) && conditionsElem.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var conditionElem in conditionsElem.EnumerateArray())
                                            {
                                                try
                                                {
                                                    rule.TaxRuleConditions.Add(new POS_UI.Models.TaxRuleConditionModel
                                                    {
                                                        Id = conditionElem.TryGetProperty("id", out var condIdProp) && condIdProp.ValueKind == JsonValueKind.Number ? condIdProp.GetInt32() : 0,
                                                        TaxRuleId = conditionElem.TryGetProperty("tax_rule_id", out var condRuleIdProp) && condRuleIdProp.ValueKind == JsonValueKind.Number ? condRuleIdProp.GetInt32() : rule.Id,
                                                        ConditionType = conditionElem.TryGetProperty("condition_type", out var typeProp) ? typeProp.GetString() : null,
                                                        ConditionValue = conditionElem.TryGetProperty("condition_value", out var valueProp) ? valueProp.GetString() : null,
                                                        MinValue = conditionElem.TryGetProperty("min_value", out var minProp) && minProp.ValueKind == JsonValueKind.Number ? minProp.GetDecimal() : 0m,
                                                        MaxValue = conditionElem.TryGetProperty("max_value", out var maxProp) && maxProp.ValueKind == JsonValueKind.Number ? maxProp.GetDecimal() : 0m,
                                                        StartDate = conditionElem.TryGetProperty("start_date", out var startProp) && startProp.ValueKind == JsonValueKind.String ? DateTime.Parse(startProp.GetString()) : DateTime.MinValue,
                                                        EndDate = conditionElem.TryGetProperty("end_date", out var endProp) && endProp.ValueKind == JsonValueKind.String ? DateTime.Parse(endProp.GetString()) : DateTime.MinValue,
                                                        CreatedAt = conditionElem.TryGetProperty("created_at", out var condCreatedProp) && condCreatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(condCreatedProp.GetString()) : DateTime.MinValue,
                                                        UpdatedAt = conditionElem.TryGetProperty("updated_at", out var condUpdatedProp) && condUpdatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(condUpdatedProp.GetString()) : DateTime.MinValue
                                                    });
                                                }
                                                catch
                                                {
                                                    // Ignore malformed condition entries
                                                }
                                            }
                                        }

                                        profile.TaxRules.Add(rule);
                                    }
                                    catch
                                    {
                                        // Ignore malformed rule entries
                                    }
                                }
                            }

                            profiles.Add(profile);
                        }
                        catch
                        {
                            // Ignore malformed profile entries
                        }
                    }

                    shop.TaxProfiles = profiles;
                }

                // Parse printer_groups if available
                if (data.TryGetProperty("printer_groups", out var printerGroupsElem) && printerGroupsElem.ValueKind == JsonValueKind.Array)
                {
                    var printerGroups = new List<POS_UI.Models.PrinterGroupModel>();
                    foreach (var groupElem in printerGroupsElem.EnumerateArray())
                    {
                        try
                        {
                            printerGroups.Add(new POS_UI.Models.PrinterGroupModel
                            {
                                Id = groupElem.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number ? idProp.GetInt32() : 0,
                                Name = groupElem.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null,
                                Description = groupElem.TryGetProperty("description", out var descProp) ? descProp.GetString() : null,
                                Status = groupElem.TryGetProperty("status", out var statusProp) && (statusProp.ValueKind == JsonValueKind.True || (statusProp.ValueKind == JsonValueKind.String && string.Equals(statusProp.GetString(), "true", StringComparison.OrdinalIgnoreCase))),
                                CreatedAt = groupElem.TryGetProperty("created_at", out var createdProp) && createdProp.ValueKind == JsonValueKind.String ? DateTime.Parse(createdProp.GetString()) : DateTime.MinValue,
                                UpdatedAt = groupElem.TryGetProperty("updated_at", out var updatedProp) && updatedProp.ValueKind == JsonValueKind.String ? DateTime.Parse(updatedProp.GetString()) : DateTime.MinValue
                            });
                        }
                        catch
                        {
                            // Ignore malformed printer group entries
                        }
                    }
                    shop.PrinterGroups = printerGroups;
                }

                return shop;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get shop details: {ex.Message}");
            }
        }

        // Method for PHP API calls that use Laravel Passport authentication
        public async Task<string> CallPhpApiAsync(string endpoint, HttpMethod method = null, object requestBody = null)
        {
            try
            {
                // Get Laravel bearer token from settings
                var laravelBearerToken = Properties.Settings.Default.LaravelBearerToken;
                if (string.IsNullOrEmpty(laravelBearerToken))
                {
                    throw new Exception("Laravel bearer token not found. Please log in again.");
                }

                // Create a new HttpClient for PHP API calls
                using var phpHttpClient = new HttpClient();
                phpHttpClient.Timeout = TimeSpan.FromMinutes(5);
                phpHttpClient.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.UserApiBaseUrl?.Trim() ?? "https://user-dev.delivergate.com");
                phpHttpClient.DefaultRequestHeaders.Accept.Clear();
                phpHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                phpHttpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", laravelBearerToken);

                // Set method (default to GET if not specified)
                method ??= HttpMethod.Get;

                HttpResponseMessage response;
                if (method == HttpMethod.Get)
                {
                    response = await phpHttpClient.GetAsync(endpoint);
                }
                else if (method == HttpMethod.Post)
                {
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    response = await phpHttpClient.PostAsync(endpoint, content);
                }
                else if (method == HttpMethod.Put)
                {
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    response = await phpHttpClient.PutAsync(endpoint, content);
                }
                else if (method == HttpMethod.Delete)
                {
                    response = await phpHttpClient.DeleteAsync(endpoint);
                }
                else
                {
                    throw new Exception($"Unsupported HTTP method: {method}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"PHP API call failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return responseBody;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PHP API call error: {ex.Message}");

                throw;
            }
        }

        private POS_UI.Models.OrderItem ParseOrderItem(JsonElement itemElem)
        {
            var item = new POS_UI.Models.OrderItem
            {
                Id = Guid.NewGuid(),
                // Support both camelCase and snake_case from API
                ApiItemId = itemElem.TryGetProperty("itemId", out var apiItemIdProp) ? apiItemIdProp.GetInt32() :
                            (itemElem.TryGetProperty("item_id", out var apiItemIdSnake) ? (apiItemIdSnake.ValueKind == JsonValueKind.String ? int.TryParse(apiItemIdSnake.GetString(), out var v) ? v : 0 : apiItemIdSnake.GetInt32()) : 0),
                Name = itemElem.TryGetProperty("itemName", out var nameProp) ? (nameProp.GetString() ?? "") :
                       (itemElem.TryGetProperty("item_name", out var nameSnake) ? (nameSnake.GetString() ?? "") : ""),
                Quantity = itemElem.TryGetProperty("quantity", out var qtyProp) ? qtyProp.GetInt32() : 1,
                // Keep ApiItemPrice as the API-provided total (cents -> currency) if present
                ApiItemPrice = itemElem.TryGetProperty("total", out var priceProp) ? priceProp.GetDecimal() / 100m :
                                (itemElem.TryGetProperty("total", out var priceProp2) && priceProp2.ValueKind == JsonValueKind.String ? (decimal.TryParse(priceProp2.GetString(), out var tp) ? tp / 100m : 0m) : 0m),
                Note = itemElem.TryGetProperty("note", out var noteProp) ? noteProp.GetString() ?? "" : "",
                ApiDiscountAmount = itemElem.TryGetProperty("discountAmount", out var discountProp) ? discountProp.GetDecimal() / 100m :
                                    (itemElem.TryGetProperty("discount_amount", out var discountSnake) ? (discountSnake.ValueKind == JsonValueKind.Number ? discountSnake.GetDecimal() / 100m : (decimal.TryParse(discountSnake.GetString(), out var dv) ? dv / 100m : 0m)) : 0m),
                // Parse item status
                OriginalStatus = itemElem.TryGetProperty("status", out var statusProp) ? (statusProp.GetString() ?? "") : 
                               (itemElem.TryGetProperty("item_status", out var itemStatusProp) ? (itemStatusProp.GetString() ?? "") : ""),
            };
            item.ItemStatus = item.OriginalStatus; // Sync ItemStatus with OriginalStatus initially

            decimal baseUnitPrice = 0m;
            // Derive per-item Price so that OrderItem.Total (Price * Quantity) is correct in UI
            try
            {
                decimal ParseMoney(JsonElement value)
                {
                    if (value.ValueKind == JsonValueKind.Number) return value.GetDecimal() / 100m;
                    if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), out var parsed)) return parsed / 100m;
                    return 0m;
                }

                decimal unitPrice = 0m;
                decimal originalUnitPrice = 0m;

                if (itemElem.TryGetProperty("original_price", out var opSnake))
                {
                    originalUnitPrice = ParseMoney(opSnake);
                }
                else if (itemElem.TryGetProperty("originalPrice", out var opCamel))
                {
                    originalUnitPrice = ParseMoney(opCamel);
                }

                // Prefer explicit price_per_item (supports number or string, camel/snake)
                if (itemElem.TryGetProperty("price_per_item", out var ppiSnake))
                {
                    unitPrice = ParseMoney(ppiSnake);
                }
                else if (itemElem.TryGetProperty("pricePerItem", out var ppiCamel))
                {
                    unitPrice = ParseMoney(ppiCamel);
                }

                // Fallback to original price value if explicit price not present
                if (unitPrice <= 0m && originalUnitPrice > 0m)
                {
                    unitPrice = originalUnitPrice;
                }

                // Fallback: derive from total/quantity when price_per_item not available
                if (unitPrice <= 0m)
                {
                    decimal totalCents = 0m;
                    if (itemElem.TryGetProperty("total", out var totalPropFromApi))
                    {
                        if (totalPropFromApi.ValueKind == JsonValueKind.Number) totalCents = totalPropFromApi.GetDecimal();
                        else if (totalPropFromApi.ValueKind == JsonValueKind.String && decimal.TryParse(totalPropFromApi.GetString(), out var totParsed)) totalCents = totParsed;
                    }
                    if (item.Quantity > 0 && totalCents > 0m)
                    {
                        unitPrice = (totalCents / 100m) / item.Quantity;
                    }
                }

                // Additional fallback: some payloads use display_price as currency string without cents scaling
                if (unitPrice <= 0m && itemElem.TryGetProperty("display_price", out var dpProp) && dpProp.ValueKind == JsonValueKind.String)
                {
                    var raw = dpProp.GetString()?.Replace(" ", "");
                    if (decimal.TryParse(raw, out var parsed)) unitPrice = parsed;
                }

                if (originalUnitPrice <= 0m)
                {
                    originalUnitPrice = unitPrice;
                }

                item.Price = Math.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
                item.BaseUnitPrice = Math.Round(originalUnitPrice > 0m ? originalUnitPrice : unitPrice, 2, MidpointRounding.AwayFromZero);
            }
            catch { /* ignore price derivation errors and leave default */ }

            // Capture API item id for update calls (support multiple possible fields)
            /*if (itemElem.TryGetProperty("id", out var apiItemIdProp) && apiItemIdProp.ValueKind == JsonValueKind.Number)
            {
                item.ApiItemId = apiItemIdProp.GetInt32();
            }
           /* else if (itemElem.TryGetProperty("item_id", out var apiItemSnake) && apiItemSnake.ValueKind == JsonValueKind.Number)
            {
                item.ApiItemId = apiItemSnake.GetInt32();
            }
            */
            // Ensure the visible discount field reflects API discount for item display in cart
            item.DisAmount = item.ApiDiscountAmount;
            item.VisibleDiscountAmount = item.ApiDiscountAmount; // Set VisibleDiscountAmount for receipt printing
            if (item.Quantity > 0 && item.ApiDiscountAmount > 0m)
            {
                item.UnitDiscountAmount = Math.Round(item.ApiDiscountAmount / item.Quantity, 2, MidpointRounding.AwayFromZero);
            }

            var aggregatedTaxDetails = ParseTaxDetailsFromElement(itemElem) ?? new List<TaxDetailModel>();
            if (aggregatedTaxDetails.Count > 0)
            {
                var baseLineTotal = Math.Round(Math.Max(0m, item.Price * item.Quantity), 2, MidpointRounding.AwayFromZero);
                ApplyFallbackTaxableAmount(aggregatedTaxDetails, baseLineTotal, true);
            }

            void AppendTaxDetails(JsonElement sourceNode, decimal fallbackTaxable = 0m, bool markComponent = false)
            {
                var details = ParseTaxDetailsFromElement(sourceNode);
                if (details != null && details.Count > 0)
                {
                    ApplyFallbackTaxableAmount(details, fallbackTaxable, true);
                    if (markComponent)
                    {
                        foreach (var detail in details)
                        {
                            if (detail != null)
                            {
                                detail.IsComponentDetail = true;
                            }
                        }
                    }
                    aggregatedTaxDetails.AddRange(details);
                }
            }

            // Parse modifiers if they exist
            if (itemElem.TryGetProperty("modifiers", out var modifiersProp) && modifiersProp.ValueKind == JsonValueKind.Array)
            {
                var selectedModifiers = new Dictionary<int, List<string>>();
                var nestedModifierDetails = new Dictionary<string, List<string>>();
                var modifierGroups = new List<POS_UI.Models.ModifierModel>();

                foreach (var modifierElem in modifiersProp.EnumerateArray())
                {
                    // Get modifier group ID and title
                    var modifierGroupId = 0;
                    if (modifierElem.TryGetProperty("item_id", out var groupIdProp))
                    {
                        if (groupIdProp.ValueKind == JsonValueKind.Number)
                        {
                            modifierGroupId = groupIdProp.GetInt32();
                        }
                        else if (groupIdProp.ValueKind == JsonValueKind.String)
                        {
                            int.TryParse(groupIdProp.GetString(), out modifierGroupId);
                        }
                    }
                    var modifierGroupTitle = modifierElem.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

                    // Create modifier group
                    var modifierGroup = new POS_UI.Models.ModifierModel
                    {
                        Id = modifierGroupId,
                        Title = modifierGroupTitle,
                        ModifierItems = new List<POS_UI.Models.ModifierItemModel>()
                    };
                    modifierGroup.IsTaxInherited = TryGetBoolean(modifierElem, "is_inherited");

                    // Parse selected items
                    if (modifierElem.TryGetProperty("selected_item", out var selectedItemsProp) && selectedItemsProp.ValueKind == JsonValueKind.Array)
                    {
                        var selectedItemNames = new List<string>();

                        foreach (var selectedItemElem in selectedItemsProp.EnumerateArray())
                        {
                            var itemName = selectedItemElem.TryGetProperty("title", out var itemNameProp) ? itemNameProp.GetString() ?? "" : "";
                            var itemPrice = 0m;
                            if (selectedItemElem.TryGetProperty("price_per_item", out var itemPriceProp))
                            {
                                if (itemPriceProp.ValueKind == JsonValueKind.Number)
                                {
                                    itemPrice = itemPriceProp.GetDecimal() / 100m;
                                }
                                else if (itemPriceProp.ValueKind == JsonValueKind.String)
                                {
                                    if (decimal.TryParse(itemPriceProp.GetString(), out var parsedPrice))
                                    {
                                        itemPrice = parsedPrice / 100m;
                                    }
                                }
                            }

                            var originalPrice = 0m;
                            if (selectedItemElem.TryGetProperty("original_price", out var originalPriceProp))
                            {
                                if (originalPriceProp.ValueKind == JsonValueKind.Number)
                                {
                                    originalPrice = originalPriceProp.GetDecimal() / 100m;
                                }
                                else if (originalPriceProp.ValueKind == JsonValueKind.String)
                                {
                                    if (decimal.TryParse(originalPriceProp.GetString(), out var parsedOrigPrice))
                                    {
                                        originalPrice = parsedOrigPrice / 100m;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(itemName))
                            {
                                selectedItemNames.Add(itemName);

                                // Create modifier item
                                var modifierItem = new POS_UI.Models.ModifierItemModel
                                {
                                    Id = modifierGroup.ModifierItems.Count + 1,
                                    ItemName = itemName,
                                    ItemPrice = itemPrice,
                                    OriginalPrice = originalPrice
                                };
                                modifierGroup.ModifierItems.Add(modifierItem);

                                var itemQuantity = TryGetDecimal(selectedItemElem, "quantity");
                                if (itemQuantity <= 0m) itemQuantity = 1m;
                                var fallbackTaxable = Math.Round(itemPrice * itemQuantity, 2, MidpointRounding.AwayFromZero);
                                AppendTaxDetails(selectedItemElem, fallbackTaxable, true);
                                var aggregatedModifierDetail = AggregateTaxDetail(ParseTaxDetailsFromElement(selectedItemElem));
                                if (aggregatedModifierDetail != null)
                                {
                                    var label = BuildModifierLabel(modifierGroupTitle, itemName);
                                    item.SetExternalModifierTaxDetail(label, aggregatedModifierDetail);
                                }

                                // Parse nested modifiers for this item
                                if (selectedItemElem.TryGetProperty("modifiers", out var nestedModifiersProp) && nestedModifiersProp.ValueKind == JsonValueKind.Array)
                                {
                                    var nestedDetails = new List<string>();

                                    foreach (var nestedModifierElem in nestedModifiersProp.EnumerateArray())
                                    {
                                        var nestedGroupTitle = nestedModifierElem.TryGetProperty("title", out var nestedTitleProp) ? nestedTitleProp.GetString() ?? "" : "";
                                        // Try to read nested group id (API requires Id in update payload)
                                        var nestedGroupId = 0;
                                        if (nestedModifierElem.TryGetProperty("item_id", out var nestedGroupIdProp))
                                        {
                                            if (nestedGroupIdProp.ValueKind == JsonValueKind.Number)
                                            {
                                                nestedGroupId = nestedGroupIdProp.GetInt32();
                                            }
                                            else if (nestedGroupIdProp.ValueKind == JsonValueKind.String)
                                            {
                                                int.TryParse(nestedGroupIdProp.GetString(), out nestedGroupId);
                                            }
                                        }

                                        // Prepare nested group model for later request building
                                        var nestedGroupModel = new POS_UI.Models.ModifierModel
                                        {
                                            Id = nestedGroupId,
                                            Title = string.IsNullOrEmpty(nestedGroupTitle) ? "" : nestedGroupTitle,
                                            ModifierItems = new List<POS_UI.Models.ModifierItemModel>()
                                        };
                                        nestedGroupModel.IsTaxInherited = TryGetBoolean(nestedModifierElem, "is_inherited");
                                        if (modifierItem.NestedModifiers == null)
                                        {
                                            modifierItem.NestedModifiers = new List<POS_UI.Models.ModifierModel>();
                                        }
                                        
                                        if (nestedModifierElem.TryGetProperty("selected_item", out var nestedSelectedItemsProp) && nestedSelectedItemsProp.ValueKind == JsonValueKind.Array)
                                        {
                                            foreach (var nestedSelectedItemElem in nestedSelectedItemsProp.EnumerateArray())
                                            {
                                                var nestedItemName = nestedSelectedItemElem.TryGetProperty("title", out var nestedItemNameProp) ? nestedItemNameProp.GetString() ?? "" : "";
                                                var nestedItemPrice = 0m;
                                                if (nestedSelectedItemElem.TryGetProperty("price_per_item", out var nestedItemPriceProp))
                                                {
                                                    if (nestedItemPriceProp.ValueKind == JsonValueKind.Number)
                                                    {
                                                        nestedItemPrice = nestedItemPriceProp.GetDecimal() / 100m;
                                                    }
                                                    else if (nestedItemPriceProp.ValueKind == JsonValueKind.String)
                                                    {
                                                        if (decimal.TryParse(nestedItemPriceProp.GetString(), out var parsedPrice))
                                                        {
                                                            nestedItemPrice = parsedPrice / 100m;
                                                        }
                                                    }
                                                }
                                                string nestedExternalItemId = null;
                                                if (nestedSelectedItemElem.TryGetProperty("external_item_id", out var nestedExtIdProp))
                                                {
                                                    nestedExternalItemId = nestedExtIdProp.ValueKind == JsonValueKind.String ? nestedExtIdProp.GetString() : null;
                                                }
                                                
                                                if (!string.IsNullOrEmpty(nestedItemName))
                                                {
                                                    nestedDetails.Add($"{nestedGroupTitle}: {nestedItemName}   ${nestedItemPrice:F2}");
                                                    var nestedQuantity = TryGetDecimal(nestedSelectedItemElem, "quantity");
                                                    if (nestedQuantity <= 0m) nestedQuantity = 1m;
                                                    var nestedFallbackTaxable = Math.Round(nestedItemPrice * nestedQuantity, 2, MidpointRounding.AwayFromZero);
                                                    AppendTaxDetails(nestedSelectedItemElem, nestedFallbackTaxable, true);
                                                    var aggregatedNestedDetail = AggregateTaxDetail(ParseTaxDetailsFromElement(nestedSelectedItemElem));
                                                    if (aggregatedNestedDetail != null)
                                                    {
                                                        var nestedLabel = BuildModifierLabel(nestedGroupTitle, nestedItemName);
                                                        item.SetExternalModifierTaxDetail(nestedLabel, aggregatedNestedDetail);
                                                    }
                                                    // Add to nested group model
                                                    nestedGroupModel.ModifierItems.Add(new POS_UI.Models.ModifierItemModel
                                                    {
                                                        Id = nestedGroupModel.ModifierItems.Count + 1,
                                                        ItemName = nestedItemName,
                                                        ItemPrice = nestedItemPrice,
                                                        ExternalItemId = nestedExternalItemId
                                                    });
                                                }
                                            }
                                        }

                                        // Only add nested group model if it has items
                                        if (nestedGroupModel.ModifierItems.Count > 0)
                                        {
                                            modifierItem.NestedModifiers.Add(nestedGroupModel);
                                        }
                                    }

                                    if (nestedDetails.Count > 0)
                                    {
                                        nestedModifierDetails[itemName] = nestedDetails;
                                    }
                                }
                            }
                        }

                        if (selectedItemNames.Count > 0)
                        {
                            selectedModifiers[modifierGroupId] = selectedItemNames;
                        }
                    }

                    modifierGroups.Add(modifierGroup);
                }

                item.SelectedModifiers = selectedModifiers;
                item.NestedModifierDetails = nestedModifierDetails;

                // Create a temporary Product object with the modifier groups for display purposes
                if (modifierGroups.Count > 0)
                {
                    // Prefer the true unit price derived earlier; fallback to line totals only when necessary
                    decimal unitPriceForProduct = item.BaseUnitPrice > 0m ? item.BaseUnitPrice : item.Price;
                    if (unitPriceForProduct <= 0m && item.Quantity > 0 && item.ApiItemPrice > 0m)
                    {
                        unitPriceForProduct = Math.Round(item.ApiItemPrice / item.Quantity, 2, MidpointRounding.AwayFromZero);
                    }
                    if (unitPriceForProduct <= 0m && item.ApiItemPrice > 0m)
                    {
                        unitPriceForProduct = item.ApiItemPrice;
                    }

                    item.Product = new POS_UI.Models.ProductItemModel
                    {
                        Id = 0,
                        ItemName = item.Name,
                        Price = unitPriceForProduct,
                        PricePerItem = unitPriceForProduct,
                        Modifiers = modifierGroups,
                        PrinterGroups = new List<POS_UI.Models.PrinterGroupModel>()
                    };

                    // Parse printer_groups from order item if they exist
                    if (itemElem.TryGetProperty("printer_groups", out var itemPrinterGroupsElement) && itemPrinterGroupsElement.ValueKind == JsonValueKind.Array)
                    {
                        var printerGroups = new List<POS_UI.Models.PrinterGroupModel>();
                        foreach (var printerGroupElement in itemPrinterGroupsElement.EnumerateArray())
                        {
                            var printerGroup = new POS_UI.Models.PrinterGroupModel
                            {
                                Id = printerGroupElement.TryGetProperty("id", out var pgIdElement) ? pgIdElement.GetInt32() : 0,
                                Name = printerGroupElement.TryGetProperty("name", out var pgNameElement) ? pgNameElement.GetString() : null,
                                Description = printerGroupElement.TryGetProperty("description", out var pgDescElement) ? pgDescElement.GetString() : null,
                                Status = printerGroupElement.TryGetProperty("status", out var pgStatusElement) && 
                                        (pgStatusElement.ValueKind == JsonValueKind.True || 
                                         (pgStatusElement.ValueKind == JsonValueKind.Number && pgStatusElement.GetInt32() == 1) ||
                                         (pgStatusElement.ValueKind == JsonValueKind.String && string.Equals(pgStatusElement.GetString(), "1", StringComparison.OrdinalIgnoreCase)))
                            };
                            
                            printerGroups.Add(printerGroup);
                        }
                        item.Product.PrinterGroups = printerGroups;
                    }

                }
            }

            if (itemElem.TryGetProperty("printer_groups", out var itemPrinterGroupsForAll) && itemPrinterGroupsForAll.ValueKind == JsonValueKind.Array)
            {
                var printerGroupsForItem = new List<POS_UI.Models.PrinterGroupModel>();
                foreach (var pgEl in itemPrinterGroupsForAll.EnumerateArray())
                {
                    var pg = new POS_UI.Models.PrinterGroupModel
                    {
                        Id = pgEl.TryGetProperty("id", out var pid) ? pid.GetInt32() : 0,
                        Name = pgEl.TryGetProperty("name", out var pname) ? pname.GetString() : null,
                        Description = pgEl.TryGetProperty("description", out var pdesc) ? pdesc.GetString() : null,
                        Status = pgEl.TryGetProperty("status", out var pstatus) &&
                                 (pstatus.ValueKind == JsonValueKind.True ||
                                  (pstatus.ValueKind == JsonValueKind.Number && pstatus.GetInt32() == 1) ||
                                  (pstatus.ValueKind == JsonValueKind.String && string.Equals(pstatus.GetString(), "1", StringComparison.OrdinalIgnoreCase)))
                    };
                    printerGroupsForItem.Add(pg);
                }
                if (printerGroupsForItem.Count > 0)
                {
                    if (item.Product == null)
                    {
                        decimal unitPriceForProduct = item.BaseUnitPrice > 0m ? item.BaseUnitPrice : item.Price;
                        if (unitPriceForProduct <= 0m && item.Quantity > 0 && item.ApiItemPrice > 0m)
                            unitPriceForProduct = Math.Round(item.ApiItemPrice / item.Quantity, 2, MidpointRounding.AwayFromZero);
                        if (unitPriceForProduct <= 0m && item.ApiItemPrice > 0m)
                            unitPriceForProduct = item.ApiItemPrice;
                        item.Product = new POS_UI.Models.ProductItemModel
                        {
                            Id = 0,
                            ItemName = item.Name,
                            Price = unitPriceForProduct,
                            PricePerItem = unitPriceForProduct,
                            Modifiers = new List<POS_UI.Models.ModifierModel>(),
                            PrinterGroups = printerGroupsForItem
                        };
                    }
                    else
                    {
                        item.Product.PrinterGroups = printerGroupsForItem;
                    }
                }
            }

            if (item.BaseUnitPrice <= 0m)
            {
                item.BaseUnitPrice = Math.Round(item.Price, 2, MidpointRounding.AwayFromZero);
            }

            if (aggregatedTaxDetails.Count > 0)
            {
                item.TaxDetails = aggregatedTaxDetails;
                item.TaxAmount = aggregatedTaxDetails.Sum(td => td.Amount);
            }

            return item;
        }

        private List<TaxDetailModel> ParseTaxDetailsFromElement(JsonElement parentElement)
        {
            if (!parentElement.TryGetProperty("tax_details", out var taxDetailsProp) || taxDetailsProp.ValueKind == JsonValueKind.Null)
            {
                return new List<TaxDetailModel>();
            }

            return ParseTaxDetailsNode(taxDetailsProp);
        }

        private List<TaxDetailModel> ParseTaxDetailsNode(JsonElement node)
        {
            var details = new List<TaxDetailModel>();
            switch (node.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var element in node.EnumerateArray())
                    {
                        var detail = ParseSingleTaxDetail(element);
                        if (detail != null)
                        {
                            details.Add(detail);
                        }
                    }
                    break;
                case JsonValueKind.Object:
                    var singleDetail = ParseSingleTaxDetail(node);
                    if (singleDetail != null)
                    {
                        details.Add(singleDetail);
                    }
                    break;
            }
            return details;
        }

        private static void ApplyFallbackTaxableAmount(List<TaxDetailModel> details, decimal fallbackAmount, bool onlyWhenSingleDetail)
        {
            if (details == null || details.Count == 0) return;
            if (fallbackAmount <= 0m) return;
            if (onlyWhenSingleDetail && details.Count != 1) return;

            foreach (var detail in details)
            {
                if (detail == null) continue;
                if (detail.TaxableAmount <= 0m)
                {
                    detail.TaxableAmount = fallbackAmount;
                }
            }
        }

        private static void MarkDetailsAsComponent(List<TaxDetailModel> details)
        {
            if (details == null) return;
            foreach (var detail in details)
            {
                if (detail == null) continue;
                detail.IsComponentDetail = true;
            }
        }

        private static TaxDetailModel AggregateTaxDetail(List<TaxDetailModel> details)
        {
            if (details == null || details.Count == 0) return null;
            var filtered = details.Where(d => d != null).ToList();
            if (filtered.Count == 0) return null;
            var primary = filtered
                .OrderByDescending(d => d.Rate)
                .ThenByDescending(d => d.Amount)
                .FirstOrDefault();
            if (primary == null) return null;

            return new TaxDetailModel
            {
                TaxProfileId = primary.TaxProfileId,
                TaxRuleId = primary.TaxRuleId,
                TaxId = primary.TaxId,
                TaxCode = primary.TaxCode,
                Rate = primary.Rate,
                Amount = Math.Round(filtered.Sum(d => d.Amount), 2, MidpointRounding.AwayFromZero),
                TaxableAmount = Math.Round(filtered.Sum(d => d.TaxableAmount), 2, MidpointRounding.AwayFromZero)
            };
        }

        private static string BuildModifierLabel(string groupTitle, string modifierName)
        {
            var label = $"{groupTitle ?? string.Empty}: {modifierName ?? string.Empty}".Trim();
            return label;
        }

        private TaxDetailModel ParseSingleTaxDetail(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var amount = TryGetDecimal(element, "amount");
            if (amount == 0m)
            {
                amount = TryGetDecimal(element, "tax_amount");
            }

            var rate = TryGetDecimal(element, "tax_rate");
            if (rate == 0m)
            {
                rate = TryGetDecimal(element, "rate");
            }
            var taxable = 0m;

            if (element.TryGetProperty("taxable_amount", out var taxableProp))
            {
                taxable = TryGetDecimal(taxableProp);
            }
            else if (element.TryGetProperty("taxable_value", out var taxableValueProp))
            {
                taxable = TryGetDecimal(taxableValueProp);
            }

            return new TaxDetailModel
            {
                TaxProfileId = TryGetNullableInt(element, "tax_profile_id"),
                TaxRuleId = TryGetNullableInt(element, "tax_rule_id"),
                TaxId = TryGetNullableInt(element, "tax_id"),
                TaxCode = element.TryGetProperty("tax_code", out var codeProp) ? codeProp.GetString() : null,
                Rate = rate,
                Amount = amount,
                TaxableAmount = taxable
            };
        }


        private static bool TryGetBoolean(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var prop))
            {
                return false;
            }

            return TryGetBoolean(prop);
        }

        private static bool TryGetBoolean(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out var numberValue))
                    {
                        return numberValue != 0;
                    }
                    if (element.TryGetDecimal(out var decimalValue))
                    {
                        return decimalValue != 0m;
                    }
                    break;
                case JsonValueKind.String:
                    var raw = element.GetString();
                    if (string.IsNullOrWhiteSpace(raw)) return false;
                    raw = raw.Trim();
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                    {
                        return parsedInt != 0;
                    }
                    return raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                           raw.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                           raw.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                           raw.Equals("1", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static int GetInt32FromElement(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var intValue))
                return intValue;
            if (element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out var parsed))
                return parsed;
            return 0;
        }

        private static int? TryGetNullableInt(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var prop) || prop.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var intValue))
            {
                return intValue;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                return parsed;
            }

            return null;
        }

        private static decimal TryGetDecimal(JsonElement parent, string propertyName)
        {
            if (!parent.TryGetProperty(propertyName, out var prop))
            {
                return 0m;
            }

            return TryGetDecimal(prop);
        }

        private static decimal TryGetDecimal(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return element.GetDecimal();
                case JsonValueKind.String:
                    var raw = element.GetString();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        raw = raw.Replace(" ", string.Empty);
                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                        {
                            return parsed;
                        }
                        if (decimal.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
                        {
                            return parsed;
                        }
                    }
                    break;
            }

            return 0m;
        }

        // Method for Laravel API calls with custom bearer token and tenant code
        public async Task<string> CallLaravelApiAsync(string endpoint, string bearerToken, string tenantCode, HttpMethod method = null, object requestBody = null)
        {
            try
            {
                if (string.IsNullOrEmpty(bearerToken))
                {
                    throw new Exception("Bearer token is required for Laravel API calls.");
                }

                // Create a new HttpClient for Laravel API calls
                using var laravelHttpClient = new HttpClient();
                laravelHttpClient.Timeout = TimeSpan.FromMinutes(5);
                laravelHttpClient.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                laravelHttpClient.DefaultRequestHeaders.Accept.Clear();
                laravelHttpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                laravelHttpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                // Add tenant code header if provided
                if (!string.IsNullOrEmpty(tenantCode))
                {
                    laravelHttpClient.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                // Set method (default to GET if not specified)
                method ??= HttpMethod.Get;

                HttpResponseMessage response;
                if (method == HttpMethod.Get)
                {
                    response = await laravelHttpClient.GetAsync(endpoint);
                }
                else if (method == HttpMethod.Post)
                {
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    response = await laravelHttpClient.PostAsync(endpoint, content);
                }
                else if (method == HttpMethod.Put)
                {
                    var json = JsonSerializer.Serialize(requestBody);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                    response = await laravelHttpClient.PutAsync(endpoint, content);
                }
                else if (method == HttpMethod.Delete)
                {
                    response = await laravelHttpClient.DeleteAsync(endpoint);
                }
                else
                {
                    throw new Exception($"Unsupported HTTP method: {method}");
                }

                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Laravel API call failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return responseBody;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Laravel API call error: {ex.Message}");
                throw;
            }
        }

        public async Task<List<POS_UI.Models.PlatformModel>> GetPlatformsAsync()
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                // Get outlet_id from shop details and brand_id from settings
                var shopDetails = GlobalDataService.Instance.ShopDetails;
                // Attempt to hydrate shop details if null (from local storage or API)
                if (shopDetails == null)
                {
                    try
                    {
                        var localStorage = new LocalStorageService();
                        var stored = localStorage.GetShopDetails();
                        if (stored != null)
                        {
                            shopDetails = stored;
                        }
                        else
                        {
                            var (_, outletCodeFromSettings, brandIdFromSettings) = _settingsService.LoadSettings();
                            if (!string.IsNullOrWhiteSpace(outletCodeFromSettings) && !string.IsNullOrWhiteSpace(brandIdFromSettings))
                            {
                                shopDetails = await GetShopDetailsAsync(outletCodeFromSettings, brandIdFromSettings);
                            }
                        }
                    }
                    catch { /* silent hydrate attempt */ }
                }

                var (_, _, brandId) = _settingsService.LoadSettings();
                

                // Prefer DeliveryPlatform.OutletId if provided, else Shop Id
                var outletId = 0;
                if (shopDetails != null)
                {
                    if (shopDetails.DeliveryPlatform != null && shopDetails.DeliveryPlatform.OutletId > 0)
                    {
                        outletId = shopDetails.DeliveryPlatform.OutletId;
                    }
                    else if (shopDetails.Id > 0)
                    {
                        outletId = shopDetails.Id;
                    }
                }
                
                if (string.IsNullOrEmpty(brandId))
                {
                    throw new Exception("Brand ID not found in settings. Please check settings.txt file.");
                }
                if (outletId <= 0)
                {
                    throw new Exception("Outlet ID is 0. Ensure shop details are loaded before calling GetPlatformsAsync.");
                }

                var url = $"/api/v1/delivery-platform?outlet_id={outletId}&brand_id={brandId}";
                //MessageBox.Show($"API Service: Making request to: {url}");
                //MessageBox.Show($"API Service: Brand ID: {brandId}");
                //MessageBox.Show($"API Service: Outlet ID: {outletId}");
                var response = await _httpClient.GetAsync(url);
                if(!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetPlatforms"); } catch { }
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                
                var platforms = new List<POS_UI.Models.PlatformModel>();
                foreach(var platformElem in data.EnumerateArray())
                {
                    // Use store_status for toggle/display instead of status
                    var storeStatusStr = platformElem.GetProperty("store_status").GetString();
                    var isOnline = string.Equals(storeStatusStr?.Trim(), "Online", StringComparison.OrdinalIgnoreCase);

                    var platform = new POS_UI.Models.PlatformModel
                    {
                        Id = platformElem.GetProperty("id").GetInt32(),
                        Name = platformElem.GetProperty("name").GetString(),
                        PlatformName = platformElem.GetProperty("platform_name").GetString(),
                        Status = storeStatusStr, // display Online/Offline
                        PlatformLogo = platformElem.GetProperty("logo").GetString(),
                        PlatformId = platformElem.GetProperty("platform_id").GetInt32(),
                        IsActive = isOnline, // toggle bound to Online/Offline
                        AutoAccepting = platformElem.GetProperty("auto_accepting").GetBoolean()
                    };
                    platforms.Add(platform);
                }
                return platforms;
            }
            catch(Exception ex)
            {
                throw new Exception($"Failed to get platforms: {ex.Message}");
            }
        }

        private async Task<string> GetUserServiceBearerTokenAsync(bool forceRefresh = false)
        {
            try
            {
                var storedToken = Properties.Settings.Default.LaravelBearerToken;
                if (forceRefresh || string.IsNullOrWhiteSpace(storedToken))
                {
                    Console.WriteLine("API Service: Laravel bearer token missing or refresh requested. Fetching new token...");
                    var laravelPassportService = new LaravelPassportService();
                    storedToken = await laravelPassportService.GetAccessTokenAsync();

                    if (string.IsNullOrWhiteSpace(storedToken))
                    {
                        throw new Exception("Laravel bearer token acquisition returned empty value.");
                    }

                    Properties.Settings.Default.LaravelBearerToken = storedToken;
                    Properties.Settings.Default.Save();
                }

                return storedToken;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"API Service: Error getting user service bearer token: {ex.Message}");
                throw;
            }
        }

        public async Task<(bool IsValid, string ErrorMessage, string VoucherValue, string ValueType)> ValidateVoucherAsync(
            string voucher,
            decimal cartValue,
            string purchaseType,
            string outletId,
            string brandId,
            IEnumerable<int> categoryIds,
            string paymentType, int customerId)
        {
            try
            {
                // Get bearer token from user service API
                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    throw new Exception("Failed to acquire bearer token from user service.");

                Console.WriteLine($"API Service: Acquired bearer token from user service successfully");
                
                // Debug: Show token info (first 20 chars for security)
                //var tokenPreview = bearerToken.Length > 20 ? bearerToken.Substring(0, 20) + "..." : bearerToken;
                //Console.WriteLine($"API Service: Token preview: {tokenPreview}");

                var (tenantCode, _, _) = _settingsService.LoadSettings();
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.AdminBaseUrl?.Trim() ?? "https://admin-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                var body = new
                {
                    cart_value = cartValue,
                    purchase_type = purchaseType,
                    outlet = outletId,
                    webshop_brand_id = brandId,
                    category_ids = categoryIds?.ToArray() ?? Array.Empty<int>(),
                    payment_type = "none",
                    customer_id = customerId
                /*// Create form data content instead of JSON to match Postman format
                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("cart_value", cartValue.ToString()),
                    new KeyValuePair<string, string>("purchase_type", purchaseType),
                    new KeyValuePair<string, string>("outlet", outletId),
                    new KeyValuePair<string, string>("webshop_brand_id", brandId),
                    new KeyValuePair<string, string>("payment_type", "none")*/
                };

                var json = JsonSerializer.Serialize(body);
                //MessageBox.Show($"API Service: Voucher validation request body: {json}");
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                /*// Add category_ids as comma-separated string if available
                if (categoryIds != null && categoryIds.Any())
                {
                    var categoryIdsString = string.Join(",", categoryIds);
                    formData.Add(new KeyValuePair<string, string>("category_ids[]", categoryIdsString));
                    Console.WriteLine($"API Service: Category IDs being sent: {categoryIdsString}");
                }

                var content = new FormUrlEncodedContent(formData);
                
                // Log the form data for debugging
                var formDataString = string.Join("&", formData.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                Console.WriteLine($"API Service: Form data being sent: {formDataString}");
                //MessageBox.Show($"API Service: Form data being sent: {formDataString}");
                */
                var url = $"/api/v1/hq/voucher/{Uri.EscapeDataString(voucher)}/validate";
                //MessageBox.Show($"API Service: Making request to: {url}");
                
                var response = await http.PostAsync(url, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                //MessageBox.Show($"API Service: Response status: {response.StatusCode}");
                //MessageBox.Show($"API Service: Response body: {responseBody}");
                //MessageBox.Show($"API Service: Response status: {response.StatusCode}");
                //MessageBox.Show($"API Service: Response body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    // Try to extract error message from response
                    string errorMessage = "Coupon validation failed";
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            errorMessage = messageElement.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            errorMessage = errorElement.GetString();
                        }
                        else if (doc.RootElement.TryGetProperty("data", out var dataElement) && 
                                 dataElement.TryGetProperty("message", out var dataMessageElement))
                        {
                            errorMessage = dataMessageElement.GetString();
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, use the raw response body
                        if (!string.IsNullOrWhiteSpace(responseBody))
                        {
                            errorMessage = responseBody;
                        }
                    }
                    
                    return (false, errorMessage, null, null);
                }

                // Parse successful response to extract voucher details
                string voucherValue = null;
                string valueType = null;
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("data", out var dataElement))
                    {
                        if (dataElement.TryGetProperty("voucher_value", out var voucherValueElement))
                        {
                            voucherValue = voucherValueElement.GetString();
                        }
                        if (dataElement.TryGetProperty("value_type", out var valueTypeElement))
                        {
                            valueType = valueTypeElement.GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error parsing voucher response: {ex.Message}");
                }

                return (true, null, voucherValue, valueType);
            }
            catch (Exception ex)
            {
                return (false, $"Error validating coupon: {ex.Message}", null, null);
            }
        }


        //////////////////////////////////////////////////////
        public async Task<(bool IsSuccess, string ErrorMessage)> UpdateDeliveryPlatformOrderActionAsync(
    int platformId,
    string autoAccepting,
    string storeStatus,
    string availableFrom = null)
{
    try
    {
        // Get bearer token from user service
        var bearerToken = await GetUserServiceBearerTokenAsync();
        if (string.IsNullOrEmpty(bearerToken))
            throw new Exception("Failed to acquire bearer token from user service.");

        var (tenantCode, _, _) = _settingsService.LoadSettings();

        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(5);
        // Align with GET base to ensure the same environment
        http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
        http.DefaultRequestHeaders.Accept.Clear();
        http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            http.DefaultRequestHeaders.Remove("x-tenant-code");
            http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
        }

        var body = new Dictionary<string, object>
        {
            { "auto_accepting", autoAccepting },
            { "store_status", storeStatus }
        };

        if (!string.IsNullOrEmpty(availableFrom))
        {
            body["available_from"] = availableFrom;
        }

       var json = JsonSerializer.Serialize(body);
       var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Query params required by backend: outlet_id and brand_id
        var shopDetails = GlobalDataService.Instance.ShopDetails;
        var (_, _, brandId) = _settingsService.LoadSettings();
        var outletId = shopDetails?.Id ?? 2;

        // Backend expects the delivery-platform resource id ("id" from GET), not platform_id
        var url = $"/api/v1/admin/delivery-platform/order_action_update/{platformId}?outlet_id={outletId}&brand_id={brandId}";
        var fullUrl = new Uri(http.BaseAddress, url).ToString();
        var response = await http.PutAsync(url, content);

        var responseBody = await response.Content.ReadAsStringAsync();

if (!response.IsSuccessStatusCode)
{
    string friendlyMessage = null;
    try
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (doc.RootElement.TryGetProperty("message", out var msg))
            friendlyMessage = msg.GetString();
    }
    catch { /* ignore parse errors */ }

    var err = friendlyMessage ?? $"{(int)response.StatusCode} {response.StatusCode}";
    return (false, $"[{(int)response.StatusCode} {response.StatusCode}] {fullUrl}\nRequest JSON: {json}");
}

        return (true, null);
    }
    catch (Exception ex)
    {
        return (false, $"Error updating delivery platform: {ex.Message}");
    }
}

        public async Task<ShiftModel> GetUserShiftsAsync(int userId, DateTime fromDate, DateTime? toDate = null)
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                // Format date as yyyy-MM-dd
                var fromDateString = fromDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var url = $"/api/v1/shift-info/user/{userId}?from={fromDateString}";
                
                // Add to date parameter if provided
                if (toDate.HasValue)
                {
                    var toDateString = toDate.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    url += $"&to={toDateString}";
                    //MessageBox.Show($"to date: {toDateString}");
                }
                //MessageBox.Show($"API Service: Getting user shifts for user {userId} from {fromDateString}");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetUserShifts"); } catch { }
                    }
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                // Check if data is null or empty
                if (data.ValueKind == JsonValueKind.Null)
                {
                    // Return empty shift data
                    return new ShiftModel
                    {
                        UserId = userId,
                        FromDate = fromDate,
                        ToDate = fromDate.AddDays(1).AddSeconds(-1),
                        OrderCount = 0,
                        TotalOrderAmount = 0
                    };
                }

                var shift = new ShiftModel
                {
                    UserId = data.GetProperty("user_id").GetInt32(),
                    FromDate = DateTime.Parse(data.GetProperty("from_date").GetString()).ToLocalTime(),
                    ToDate = DateTime.Parse(data.GetProperty("to_date").GetString()).ToLocalTime(),
                    OrderCount = data.GetProperty("order_count").GetInt32(),
                    TotalCashAmount = data.GetProperty("total_cash_amount").GetDecimal(),
                    TotalCardAmount = data.GetProperty("total_card_amount").GetDecimal(),
                    TotalOrderAmount = data.GetProperty("total_order_amount").GetDecimal()
                };

                // Parse shift details
                if (data.TryGetProperty("shift_details", out var shiftDetailsProp) && shiftDetailsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var shiftDetailElem in shiftDetailsProp.EnumerateArray())
                    {
                        var shiftDetail = new ShiftDetailModel
                        {
                            ShiftId = shiftDetailElem.GetProperty("shift_id").GetInt32(),
                            ActiveShift = shiftDetailElem.GetProperty("active_shift").GetBoolean(),
                            LoginTime = DateTime.Parse(shiftDetailElem.GetProperty("login_time").GetString()).ToLocalTime(),
                            ShiftDuration = shiftDetailElem.GetProperty("shift_duration").GetString(),
                            OrderCount = shiftDetailElem.GetProperty("order_count").GetInt32(),
                            TotalAmount = shiftDetailElem.GetProperty("total_amount").GetDecimal(),
                            //TotalCashAmount = shiftDetailElem.GetProperty("total_cash_amount").GetDecimal(),
                            //TotalCardAmount = shiftDetailElem.GetProperty("total_card_amount").GetDecimal()
                        };

                        // Parse logout time if it exists
                        if (shiftDetailElem.TryGetProperty("logout_time", out var logoutTimeProp) && logoutTimeProp.ValueKind != JsonValueKind.Null)
                        {
                            shiftDetail.LogoutTime = DateTime.Parse(logoutTimeProp.GetString()).ToLocalTime();
                        }

                        shift.ShiftDetails.Add(shiftDetail);
                    }
                }

                return shift;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get user shifts: {ex.Message}");
            }
        }

        public async Task<ShopShiftInfoModel> GetShopShiftInfoAsync(int shopId, DateTime fromDate, DateTime? toDate = null)
        {
            try
            {
                // Set bearer token from settings
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    SetBearerToken(accessToken);
                }

                // Format date as full timestamp in UTC (yyyy-MM-dd HH:mm:ss)
                var fromDateString = fromDate.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                var url = $"/api/v1/shift-info/shop/{shopId}?from={fromDateString}";
                if (toDate.HasValue)
                {
                    var toDateString = toDate.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    url += $"&to={toDateString}";
                    //MessageBox.Show($"to date: {toDateString}");
                }
               // MessageBox.Show($"API Service: Getting shop shift info for shop {shopId} from {fromDateString}");
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetShopShiftInfo"); } catch { }
                    }
                    throw new Exception($"Status: {response.StatusCode}\n{error}");
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");

                var shopShiftInfo = new ShopShiftInfoModel
                {
                    ShopId = data.GetProperty("shop_id").GetInt32(),
                    FromDate = DateTime.Parse(data.GetProperty("from_date").GetString()).ToLocalTime(),
                    ToDate = DateTime.Parse(data.GetProperty("to_date").GetString()).ToLocalTime(),
                    OrderCount = data.GetProperty("order_count").GetInt32(),
                    TotalCashAmount = data.GetProperty("total_cash_amount").GetDecimal(),
                    TotalCardAmount = data.GetProperty("total_card_amount").GetDecimal(),
                    TotalOrderAmount = data.GetProperty("total_order_amount").GetDecimal()
                };

                // Parse shift details
                if (data.TryGetProperty("shift_details", out var shiftDetailsProp) && shiftDetailsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var shiftDetailElem in shiftDetailsProp.EnumerateArray())
                    {
                        var shiftDetail = new ShopShiftDetailModel
                        {
                            UserId = shiftDetailElem.GetProperty("user_id").GetInt32(),
                            OrderCount = shiftDetailElem.GetProperty("order_count").GetInt32(),
                            CashAmount = shiftDetailElem.GetProperty("cash_amount").GetDecimal(),
                            CardAmount = shiftDetailElem.GetProperty("card_amount").GetDecimal(),
                            TotalAmount = shiftDetailElem.GetProperty("total_amount").GetDecimal()
                        };

                        shopShiftInfo.ShiftDetails.Add(shiftDetail);
                    }
                }

                return shopShiftInfo;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get shop shift info: {ex.Message}");
            }
        }
 // Notify delivery platform order moved to preparing for Uber(1), Deliveroo(2), Webshop(6)
        // remoteOrderId is the third-party order ID we must forward.
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyPreparingToDeliveryPlatformAsync(string remoteOrderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");

                // Acquire bearer token used for other delivery platform calls
                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                // GET shop context headers
                var (tenantCode, _, brandId) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }
                //MessageBox.Show($"Notifying delivery platform preparing for order {remoteOrderId}");
                // Forward to delivery platform preparing endpoint
                var url = $"/api/v1/preparing/{Uri.EscapeDataString(remoteOrderId)}";
                var response = await http.PostAsync(url, null);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Notify delivery platform order is ready for pickup for Uber(1), Deliveroo(2), Webshop(6)
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyReadyToPickupToDeliveryPlatformAsync(string remoteOrderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");

                // Acquire bearer token used for other delivery platform calls
                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                // GET shop context headers
                var (tenantCode, _, brandId) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }
                //MessageBox.Show($"Notifying delivery platform ready to pickup for order {remoteOrderId}");
                // Forward to delivery platform ready-to-pickup endpoint
                var url = $"/api/v1/ready-to-pickup/{Uri.EscapeDataString(remoteOrderId)}";
                var response = await http.PostAsync(url, null);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Notify delivery platform order is served (Serve Order) for Webshop etc.
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyServeOrderToDeliveryPlatformAsync(string remoteOrderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");

                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var (tenantCode, _, brandId) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                var url = $"/api/v1/serve-order/{Uri.EscapeDataString(remoteOrderId)}";
                var response = await http.PostAsync(url, null);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Notify delivery platform order is completed for Uber(1), Deliveroo(2), Webshop(6)
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyCompleteOrderToDeliveryPlatformAsync(
            string remoteOrderId,
            string paymentMode = null,
            IReadOnlyList<DeliveryPlatformSplitPaymentLine> splitPayment = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");

                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var (tenantCode, _, brandId) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                var url = $"/api/v1/complete-order/{Uri.EscapeDataString(remoteOrderId)}";

                string json;
                if (splitPayment != null && splitPayment.Count > 0)
                {
                    var requestBody = new
                    {
                        payment_mode = "SPLIT",
                        split_payment = splitPayment.Select(p => new
                        {
                            paying_amount = p.PayingAmount,
                            payment_mode = p.PaymentMode ?? "CASH"
                        }).ToList()
                    };
                    json = JsonSerializer.Serialize(requestBody);
                }
                else
                {
                    var requestBody = new
                    {
                        payment_mode = paymentMode ?? "unknown"
                    };
                    json = JsonSerializer.Serialize(requestBody);
                }
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await http.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Notify delivery platform order is cancelled for Uber(1), Deliveroo(2), Webshop(6)
        // Refund parameters are optional - only included if provided
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyCancelOrderToDeliveryPlatformAsync(
            string remoteOrderId, 
            string cancellationReason = null,
            decimal? refundAmount = null,
            string refundMode = null,
            string refundReason = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");

                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var (tenantCode, _, _) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }
                
                // Convert cancellation reason to uppercase with underscores
                string reasonCode = null;
                if (!string.IsNullOrWhiteSpace(cancellationReason))
                {
                    // Convert "Restaurant closed" -> "RESTAURANT_CLOSED", etc.
                    reasonCode = cancellationReason.ToUpper().Replace(" ", "_");
                }
                
                //MessageBox.Show($"Notifying delivery platform cancel order for {remoteOrderId}");
                var url = $"/api/v1/cancel-order/{Uri.EscapeDataString(remoteOrderId)}";
                
                // Send as multipart/form-data with key "0" and the reason code
                var formData = new MultipartFormDataContent();
                if (!string.IsNullOrWhiteSpace(reasonCode))
                {
                    formData.Add(new StringContent(reasonCode), "0");
                }
                
                // Add optional refund parameters only if provided
                if (refundAmount.HasValue && refundAmount.Value > 0)
                {
                    formData.Add(new StringContent(refundAmount.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)), "refund[amount]");
                }
                
                if (!string.IsNullOrWhiteSpace(refundMode))
                {
                    formData.Add(new StringContent(refundMode.ToUpperInvariant()), "refund[mode]");
                }
                
                if (!string.IsNullOrWhiteSpace(refundReason))
                {
                    formData.Add(new StringContent(refundReason), "refund[reason]");
                }
                
                var response = await http.PostAsync(url, formData);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {body}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        // Update delivery platform order ready time (in minutes)
        // POST https://platform-dev.delivergate.com/api/v1/update-ready-time/{remoteOrderId}
        // Body: { "ready_in": <minutes> }
        public async Task<(bool IsSuccess, string ErrorMessage)> NotifyUpdateReadyTimeToDeliveryPlatformAsync(string remoteOrderId, int readyInMinutes)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                    return (false, "remote_order_id is required");
                if (readyInMinutes <= 0)
                    return (false, "ready_in must be greater than 0");

                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    return (false, "Failed to acquire bearer token");

                using var http = new HttpClient();
                http.BaseAddress = new Uri("https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                var (tenantCode, _, _) = _settingsService.LoadSettings();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                var url = $"/api/v1/update-ready-time/{Uri.EscapeDataString(remoteOrderId)}";
                // Some platforms expect ready_in as a string. Send as string to avoid validation errors.
                var body = new { ready_in = readyInMinutes.ToString() };
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await http.PostAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    var resp = await response.Content.ReadAsStringAsync();
                    return (false, $"{(int)response.StatusCode} {response.StatusCode}: {resp}");
                }
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public async Task<bool> UpdateUserPinAsync(int userId, string newPin)
        {
            try
            {
                // Ensure bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to update user PIN.");
                }

                SetBearerToken(accessToken);

                // Create request body
                var requestBody = new
                {
                    new_pin = newPin
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var endpoint = $"/api/v1/users/{userId}/pin";
                var response = await _httpClient.PatchAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on UpdateUserPin"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
               // MessageBox.Show($"Error updating user PIN: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> UpdateOrderPaymentAsync(int orderId, List<PaymentModel> payments)
        {
            try
            {
                // Ensure bearer token
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to update order payment.");
                }

                SetBearerToken(accessToken);

                // Create request body
                var requestBody = new
                {
                    payments = payments.Select(p => new
                    {
                        paying_amount = p.PayingAmount,
                        payment_method = p.PaymentMethod,
                        cash = p.Cash,
                        balance = p.Balance,
                        transaction_id = p.TransactionId ?? ""
                    }).ToList()
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var endpoint = $"/api/v1/orders/{orderId}/payment";
                var response = await _httpClient.PatchAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on UpdateOrderPayment"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error updating order payment: {ex.Message}");
                throw;
            }
        }

        
        // POST /api/v1/temp-payments — register a temporary payment for an order (Go POS API).
        public async Task<(int Code, string Message)> CreateTempPaymentAsync(string typeId, string paymentType, string paymentMode, decimal paymentAmount, string transactionId = "")
        {
            if (string.IsNullOrWhiteSpace(typeId))
                throw new ArgumentException("type_id is required.", nameof(typeId));
            if (string.IsNullOrWhiteSpace(paymentType))
                throw new ArgumentException("type is required.", nameof(paymentType));

            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                throw new Exception("Please log in first to create temp payment.");

            RefreshHeadersFromSettings();
            SetBearerToken(accessToken);

            var requestBody = new
            {
                type_id = typeId,
                type = paymentType.Trim().ToUpperInvariant(),
                payment_mode = paymentMode ?? "",
                payment_amount = paymentAmount,
                transaction_id = transactionId ?? ""
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/v1/temp-payments", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    try { new TokenValidationService().LogoutAndNavigateToLogin("401 on CreateTempPayment"); } catch { }
                    throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                }
                throw new Exception($"Temp payment request failed. Status: {response.StatusCode}\n{responseBody}");
            }

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.Number
                ? codeEl.GetInt32()
                : 200;
            var message = root.TryGetProperty("message", out var msgEl) ? (msgEl.GetString() ?? "success") : "success";
            return (code, message);
        }

        /// <summary>GET /api/v1/temp-payments/{displayOrderId} — list temporary payments for an order (display_order_id).</summary>
        public async Task<(int Code, string Message, List<TempPaymentRecord> Data)> GetTempPaymentsByDisplayOrderIdAsync(string displayOrderId)
        {
            if (string.IsNullOrWhiteSpace(displayOrderId))
                throw new ArgumentException("displayOrderId is required.", nameof(displayOrderId));

            var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
            if (string.IsNullOrEmpty(accessToken))
                throw new Exception("Please log in first to get temp payments.");

            RefreshHeadersFromSettings();
            SetBearerToken(accessToken);

            var idSegment = Uri.EscapeDataString(displayOrderId.Trim());
            var response = await _httpClient.GetAsync($"/api/v1/temp-payments/{idSegment}");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetTempPayments"); } catch { }
                    throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                }
                throw new Exception($"Get temp payments failed. Status: {response.StatusCode}\n{responseBody}");
            }

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var envelope = JsonSerializer.Deserialize<TempPaymentsListApiResponse>(responseBody, opts);
            if (envelope == null)
                return (200, "success", new List<TempPaymentRecord>());

            var list = envelope.Data ?? new List<TempPaymentRecord>();
            return (envelope.Code, envelope.Message ?? "success", list);
        }

        public async Task<bool> OpenCashDrawerSessionAsync(decimal openingBalance = 0)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to start a shift.");
                }

                // Resolve headers
                var (tenantCode, _, _) = _settingsService.LoadSettings();

                // Prepare request with required headers
                var endpoint = "/api/v1/cash-drawer/open-session";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                // Request body with opening_balance
                var requestBody = new
                {
                    opening_balance = openingBalance
                };
                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on OpenCashDrawerSession"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error starting shift: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CloseCashDrawerSessionAsync(decimal countedBalance)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to end a shift.");
                }

                // Resolve headers
                var (tenantCode, _, _) = _settingsService.LoadSettings();

                // Prepare request with required headers
                var endpoint = "/api/v1/cash-drawer/close-session";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                
                // Create request body with counted_balance
                var requestBody = new { counted_balance = countedBalance };
                var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on CloseCashDrawerSession"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                //MessageBox.Show($"Error ending shift: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> RecordCashMovementAsync(string movementType, decimal amount, string note = null)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to record cash movement.");
                }

                var (tenantCode, _, _) = _settingsService.LoadSettings();

                var endpoint = "/api/v1/cash-drawer/record-movement";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var body = new { movement_type = movementType, amount = amount, note = note };
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {   
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on RecordCashMovement"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch
            {
                throw;
            }
        }

        /// <summary>
        /// Logs user activity to the admin activity endpoint.
        /// </summary>
        public async Task<bool> LogUserActivityAsync(string eventType, string subject, int subjectId, int? causerId = null, string description = null)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to log user activity.");
                }

                // Get causer_id from current user if not provided
                if (!causerId.HasValue)
                {
                    var localStorage = new LocalStorageService();
                    var currentUser = localStorage.GetCurrentUser();
                    if (currentUser == null)
                    {
                        // Try to get from API if not in local storage
                        try
                        {
                            currentUser = await GetCurrentUserAsync();
                            if (currentUser != null)
                            {
                                localStorage.SaveCurrentUser(currentUser);
                            }
                        }
                        catch
                        {
                            throw new Exception("Unable to retrieve current user information.");
                        }
                    }
                    causerId = currentUser?.Id ?? throw new Exception("Current user ID not found.");
                }

                var (tenantCode, _, _) = _settingsService.LoadSettings();

                var endpoint = "/api/v1/admin/activity";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Use Dictionary to handle 'event' keyword properly in JSON
                var body = new Dictionary<string, object>
                {
                    { "event", eventType },
                    { "subject", subject },
                    { "subject_id", subjectId },
                    { "causer_id", causerId.Value }
                };
                if (!string.IsNullOrWhiteSpace(description))
                {
                    body.Add("description", description);
                }
                var json = System.Text.Json.JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {   
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on LogUserActivity"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch
            {
                throw;
            }
        }

        public async Task<POS_UI.Models.CashDrawerActiveSessionModel> GetActiveCashDrawerSessionAsync(bool xReport = false)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to fetch active cash drawer session.");
                }

                var (tenantCode, _, _) = _settingsService.LoadSettings();

                // Build endpoint with optional x_report query parameter
                var endpoint = "/api/v1/cash-drawer/active-session";
                if (xReport)
                {
                    endpoint += "?x_report=true";
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // 404 means no active session
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetActiveCashDrawerSession"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                var data = root.GetProperty("data");

                POS_UI.Models.CashDrawerActiveSessionModel model = new POS_UI.Models.CashDrawerActiveSessionModel
                {
                    Id = data.GetProperty("id").GetInt32(),
                    CashDrawerId = data.GetProperty("cash_drawer_id").GetInt32(),
                    SessionStartedUserId = data.GetProperty("session_started_user_id").GetInt32(),
                    SessionStartedUser = data.GetProperty("session_started_user_name").GetString(),
                    OpenedAt = data.GetProperty("opened_at").GetDateTime().ToLocalTime(),
                    OpeningBalance = data.GetProperty("opening_balance").GetDecimal(),
                    ClosingBalanceExpected = data.GetProperty("closing_balance_expected").GetDecimal(),
                    TotalInAmount = data.GetProperty("total_in_amount").GetDecimal(),
                    TotalOutAmount = data.GetProperty("total_out_amount").GetDecimal(),
                    TotalSalesAmount = data.GetProperty("total_sales_amount").GetDecimal(),
                    TotalRefundAmount = data.GetProperty("total_refund_amount").GetDecimal(),
                    TotalCashSaleCashRefundAmount = data.TryGetProperty("total_cash_sale_cash_refund_amount", out var cashSaleCashRefund) && cashSaleCashRefund.ValueKind != JsonValueKind.Null ? cashSaleCashRefund.GetDecimal() : 0m,
                    TotalCardSaleCashRefundAmount = data.TryGetProperty("total_card_sale_cash_refund_amount", out var cardSaleCashRefund) && cardSaleCashRefund.ValueKind != JsonValueKind.Null ? cardSaleCashRefund.GetDecimal() : 0m,
                    TotalOtherCashSaleCashRefundAmount = data.TryGetProperty("total_other_cash_sale_cash_refund_amount", out var otherCashSaleCashRefund) && otherCashSaleCashRefund.ValueKind != JsonValueKind.Null ? otherCashSaleCashRefund.GetDecimal() : 0m,
                    Status = data.GetProperty("status").GetString(),
                    CreatedAt = data.GetProperty("created_at").GetDateTime(),
                    UpdatedAt = data.GetProperty("updated_at").GetDateTime(),
                    OtherSalesAmount = data.GetProperty("total_other_sales_amount").GetDecimal()
                };

                if (data.TryGetProperty("incomplete_orders", out var incompleteOrdersEl)
                    && incompleteOrdersEl.ValueKind == JsonValueKind.Object)
                {
                    var info = new POS_UI.Models.IncompleteOrdersInfo();
                    if (incompleteOrdersEl.TryGetProperty("count", out var countEl) && countEl.ValueKind != JsonValueKind.Null)
                    {
                        info.Count = countEl.GetInt32();
                    }
                    if (incompleteOrdersEl.TryGetProperty("orders", out var ordersEl)
                        && ordersEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var orderId in ordersEl.EnumerateArray())
                        {
                            if (orderId.ValueKind == JsonValueKind.String)
                            {
                                var s = orderId.GetString();
                                if (!string.IsNullOrEmpty(s))
                                {
                                    info.Orders.Add(s);
                                }
                            }
                        }
                    }
                    model.IncompleteOrders = info;
                }

                return model;
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<POS_UI.Models.CashDrawerSessionModel>> GetCashDrawerSessionsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                 var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to fetch active cash drawer session.");
                }
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = "/api/v1/cash-drawer/";
                var queryParams = new List<string>();

                if (fromDate.HasValue)
                {
                    queryParams.Add($"from={fromDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                }

                if (toDate.HasValue)
                {
                    queryParams.Add($"to={toDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                }

                if (queryParams.Any())
                {
                    endpoint += "?" + string.Join("&", queryParams);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                using var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetCashDrawerSessions"); } catch { }
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{response.Content.ReadAsStringAsync()}");
                }
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var sessions = new List<POS_UI.Models.CashDrawerSessionModel>();

                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var session = new POS_UI.Models.CashDrawerSessionModel
                        {
                            Id = item.GetProperty("id").GetInt32(),
                            CashDrawerId = item.GetProperty("cash_drawer_id").GetInt32(),
                            SessionStartedUserId = item.GetProperty("session_started_user_id").GetInt32(),
                            SessionStartedUser = item.GetProperty("session_started_user_name").GetString(),
                            SessionEndedUser = item.GetProperty("session_ended_user_name").GetString(),
                            SessionEndedUserId = item.TryGetProperty("session_ended_user_id", out var endedUserId) && endedUserId.ValueKind != JsonValueKind.Null ? endedUserId.GetInt32() : null,
                            OpenedAt = item.GetProperty("opened_at").GetDateTime().ToLocalTime(),
                            OpeningBalance = item.GetProperty("opening_balance").GetDecimal(),
                            ClosedAt = item.TryGetProperty("closed_at", out var closedAt) && closedAt.ValueKind != JsonValueKind.Null ? closedAt.GetDateTime().ToLocalTime() : null,
                            ClosingBalanceCounted = item.TryGetProperty("closing_balance_counted", out var counted) && counted.ValueKind != JsonValueKind.Null ? counted.GetDecimal() : null,
                            ClosingBalanceExpected = item.GetProperty("closing_balance_expected").GetDecimal(),
                            Difference = item.GetProperty("difference").GetDecimal(),
                            TotalInAmount = item.GetProperty("total_in_amount").GetDecimal(),
                            TotalOutAmount = item.GetProperty("total_out_amount").GetDecimal(),
                            TotalSalesAmount = item.GetProperty("total_sales_amount").GetDecimal(),
                            TotalRefundAmount = item.GetProperty("total_refund_amount").GetDecimal(),
                            TotalCashSaleCashRefundAmount = item.TryGetProperty("total_cash_sale_cash_refund_amount", out var cashSaleCashRefund) && cashSaleCashRefund.ValueKind != JsonValueKind.Null ? cashSaleCashRefund.GetDecimal() : 0m,
                            TotalCardSaleCashRefundAmount = item.TryGetProperty("total_card_sale_cash_refund_amount", out var cardSaleCashRefund) && cardSaleCashRefund.ValueKind != JsonValueKind.Null ? cardSaleCashRefund.GetDecimal() : 0m,
                            TotalOtherCashSaleCashRefundAmount = item.TryGetProperty("total_other_cash_sale_cash_refund_amount", out var otherCashSaleCashRefund) && otherCashSaleCashRefund.ValueKind != JsonValueKind.Null ? otherCashSaleCashRefund.GetDecimal() : 0m,
                            Status = item.GetProperty("status").GetString(),
                            CreatedAt = item.GetProperty("created_at").GetDateTime().ToLocalTime(),
                            UpdatedAt = item.GetProperty("updated_at").GetDateTime().ToLocalTime(),
                            OtherSalesAmount = item.GetProperty("total_other_sales_amount").GetDecimal()
                        };

                        sessions.Add(session);
                    }
                }

                return sessions;
            }
            catch
            {
                throw;
            }
        }

        public async Task<POS_UI.Models.CashDrawerSessionModel> GetCashDrawerSessionByIdAsync(int sessionId)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to fetch cash drawer session.");
                }

                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = $"/api/v1/cash-drawer/sessions/{sessionId}";

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetCashDrawerSessionById"); } catch { }
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        throw new Exception($"Cash drawer session with ID {sessionId} not found.");
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                using var doc = System.Text.Json.JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                var data = root.GetProperty("data");

                var session = new POS_UI.Models.CashDrawerSessionModel
                {
                    Id = data.GetProperty("id").GetInt32(),
                    CashDrawerId = data.GetProperty("cash_drawer_id").GetInt32(),
                    SessionStartedUserId = data.GetProperty("session_started_user_id").GetInt32(),
                    SessionStartedUser = data.GetProperty("session_started_user_name").GetString(),
                    SessionEndedUser = data.GetProperty("session_ended_user_name").GetString(),
                    SessionEndedUserId = data.TryGetProperty("session_ended_user_id", out var endedUserId) && endedUserId.ValueKind != JsonValueKind.Null ? endedUserId.GetInt32() : null,
                    OpenedAt = data.GetProperty("opened_at").GetDateTime().ToLocalTime(),
                    OpeningBalance = data.GetProperty("opening_balance").GetDecimal(),
                    ClosedAt = data.TryGetProperty("closed_at", out var closedAt) && closedAt.ValueKind != JsonValueKind.Null ? closedAt.GetDateTime().ToLocalTime() : null,
                    ClosingBalanceCounted = data.TryGetProperty("closing_balance_counted", out var counted) && counted.ValueKind != JsonValueKind.Null ? counted.GetDecimal() : null,
                    ClosingBalanceExpected = data.GetProperty("closing_balance_expected").GetDecimal(),
                    Difference = data.GetProperty("difference").GetDecimal(),
                    TotalInAmount = data.GetProperty("total_in_amount").GetDecimal(),
                    TotalOutAmount = data.GetProperty("total_out_amount").GetDecimal(),
                    TotalSalesAmount = data.GetProperty("total_sales_amount").GetDecimal(),
                    TotalRefundAmount = data.GetProperty("total_refund_amount").GetDecimal(),
                    Status = data.GetProperty("status").GetString(),
                    CreatedAt = data.GetProperty("created_at").GetDateTime().ToLocalTime(),
                    UpdatedAt = data.GetProperty("updated_at").GetDateTime().ToLocalTime(),
                    TotalCashSaleCashRefundAmount = data.TryGetProperty("total_cash_sale_cash_refund_amount", out var cashSaleCashRefundById) && cashSaleCashRefundById.ValueKind != JsonValueKind.Null ? cashSaleCashRefundById.GetDecimal() : 0m,
                    TotalCardSaleCashRefundAmount = data.TryGetProperty("total_card_sale_cash_refund_amount", out var cardSaleCashRefundById) && cardSaleCashRefundById.ValueKind != JsonValueKind.Null ? cardSaleCashRefundById.GetDecimal() : 0m,
                    TotalOtherCashSaleCashRefundAmount = data.TryGetProperty("total_other_cash_sale_cash_refund_amount", out var otherCashSaleCashRefundById) && otherCashSaleCashRefundById.ValueKind != JsonValueKind.Null ? otherCashSaleCashRefundById.GetDecimal() : 0m,
                    OtherSalesAmount = data.GetProperty("total_other_sales_amount").GetDecimal()
                };

                return session;
            }
            catch
            {
                throw;
            }
        }

        public async Task<List<POS_UI.Models.CashDrawerTransactionModel>> GetCashDrawerTransactionsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to fetch cash drawer transactions.");
                }
                
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = "/api/v1/cash-drawer/transactions";
                var queryParams = new List<string>();

                if (fromDate.HasValue)
                {
                    queryParams.Add($"from={fromDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                }

                if (toDate.HasValue)
                {
                    queryParams.Add($"to={toDate.Value.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
                }

                if (queryParams.Any())
                {
                    endpoint += "?" + string.Join("&", queryParams);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }
                
                using var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetCashDrawerTransactions"); } catch { }
                    }
                    throw new Exception($"API request failed. Status: {response.StatusCode}\n{json}");
                }
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                var transactions = new List<POS_UI.Models.CashDrawerTransactionModel>();

                if (root.TryGetProperty("data", out var dataArray) && dataArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in dataArray.EnumerateArray())
                    {
                        var transaction = new POS_UI.Models.CashDrawerTransactionModel
                        {
                            CashDrawerId = item.GetProperty("cash_drawer_id").GetInt32(),
                            CashDrawerSessionId = item.GetProperty("cash_drawer_session_id").GetInt32(),
                            CashMovementId = item.GetProperty("cash_movement_id").GetInt32(),
                            CreatedAt = item.GetProperty("created_at").GetDateTime().ToLocalTime(),
                            MovementType = item.GetProperty("movement_type").GetString(),
                            Amount = item.GetProperty("amount").GetDecimal(),
                            UserName = item.GetProperty("user_name").GetString(),
                            Note = item.GetProperty("note").GetString() ?? "---"
                        };

                        transactions.Add(transaction);
                    }
                }

                return transactions;
            }
            catch
            {
                throw;
            }
        }


        public async Task<POS_UI.Models.SessionOrdersResponse> GetSessionOrdersAsync(int sessionId)
        {
            try
            {
                // Get bearer token from user service API
                var bearerToken = await GetUserServiceBearerTokenAsync();
                if (string.IsNullOrEmpty(bearerToken))
                    throw new Exception("Failed to acquire bearer token from user service.");

                var (tenantCode, _, _) = _settingsService.LoadSettings();

                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                var endpoint = $"/api/v1/admin/orders/session-orders?session_id={sessionId}";
                var response = await http.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get session orders. Status: {response.StatusCode}\n{responseBody}");
                }

                // Parse JSON response
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                var result = new POS_UI.Models.SessionOrdersResponse
                {
                    Message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null,
                    Code = root.TryGetProperty("code", out var code) ? code.GetInt32() : 0
                };

                if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
                {
                    result.Data = ParseSessionOrdersData(dataElement);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetSessionOrdersAsync error: {ex.Message}");
                throw;
            }
        }

        private POS_UI.Models.SessionOrdersData ParseSessionOrdersData(JsonElement dataElement)
        {
            var data = new POS_UI.Models.SessionOrdersData();

            if (dataElement.TryGetProperty("total_amount", out var totalAmount)) data.TotalAmount = totalAmount.GetString();
            if (dataElement.TryGetProperty("status", out var status)) data.Status = status.GetString();
            if (dataElement.TryGetProperty("payment_status", out var paymentStatus)) data.PaymentStatus = paymentStatus.GetString();
            if (dataElement.TryGetProperty("session_status", out var sessionStatus)) data.SessionStatus = sessionStatus.GetString();
            if (dataElement.TryGetProperty("order_details", out var orderDetails) && orderDetails.ValueKind == JsonValueKind.Array)
            {
                data.OrderDetails = orderDetails.EnumerateArray().Select(od => ParseSessionOrderDetail(od)).ToList();
            }

            return data;
        }

        private POS_UI.Models.SessionOrderDetail ParseSessionOrderDetail(JsonElement detailElement)
        {
            var detail = new POS_UI.Models.SessionOrderDetail();

            if (detailElement.TryGetProperty("display_order_id", out var displayOrderId)) detail.DisplayOrderId = displayOrderId.GetString();
            if (detailElement.TryGetProperty("status", out var status)) detail.Status = status.GetString();
            if (detailElement.TryGetProperty("order_id", out var orderApiId)) detail.OrderApiId = orderApiId.GetInt32();
            if (detailElement.TryGetProperty("total_amount", out var totalAmount))
            {
                if (totalAmount.ValueKind == JsonValueKind.Number) detail.TotalAmount = totalAmount.GetDecimal();
                else if (totalAmount.ValueKind == JsonValueKind.String && decimal.TryParse(totalAmount.GetString(), out var parsed)) detail.TotalAmount = parsed;
            }
            if (detailElement.TryGetProperty("table_id", out var tableIdEl)) detail.TableId = GetInt32FromElement(tableIdEl);

            if (detailElement.TryGetProperty("platform_name", out var platformName)) detail.PlatformName = platformName.GetString();
            if (string.IsNullOrWhiteSpace(detail.PlatformName) && detailElement.TryGetProperty("delivery_platform_name", out var deliveryPlatformName))
                detail.PlatformName = deliveryPlatformName.GetString();
            if (detailElement.TryGetProperty("platform_logo", out var platformLogo)) detail.PlatformLogo = platformLogo.GetString();
            if (detailElement.TryGetProperty("payment_status", out var paymentStatus)) detail.PaymentStatus = paymentStatus.GetString();
            if (detailElement.TryGetProperty("shipping_method", out var shippingMethod)) detail.ShippingMethod = shippingMethod.GetString();
            if (detailElement.TryGetProperty("table_order_method", out var tableOrderMethod)) detail.TableOrderMethod = tableOrderMethod.GetString();
            if (detailElement.TryGetProperty("api_status", out var apiStatus)) detail.ApiStatus = apiStatus.GetString();
            if (detailElement.TryGetProperty("created_at", out var createdAtEl) && createdAtEl.ValueKind == JsonValueKind.String
                && DateTime.TryParse(createdAtEl.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var createdAt))
                detail.CreatedAt = createdAt.ToLocalTime();
           if (detailElement.TryGetProperty("delivery_date_time", out var deliveryDtEl) && deliveryDtEl.ValueKind == JsonValueKind.String)
                {
                    var value = deliveryDtEl.GetString();

                    if (DateTime.TryParseExact(
                            value,
                            "dd/MM/yyyy hh:mm tt",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None,
                            out var deliveryDt))
                    {
                        detail.DeliveryDateTime = deliveryDt; // already local time
                    }
                }
            if (detailElement.TryGetProperty("customer_name", out var customerName)) detail.CustomerName = customerName.GetString();
            if (string.IsNullOrWhiteSpace(detail.CustomerName) && detailElement.TryGetProperty("full_name", out var fullName)) detail.CustomerName = fullName.GetString();

            return detail;
        }

        /// <summary>Gets all table IDs from session orders for the given session. Used when completing a table order to pass all session table IDs to UpdateTableStatusAsync.</summary>
        public async Task<int[]> GetTableIdsFromSessionAsync(int sessionId)
        {
            var response = await GetSessionOrdersAsync(sessionId);
            var tableIds = new List<int>();
            if (response?.Data?.OrderDetails != null)
            {
                foreach (var od in response.Data.OrderDetails)
                {
                    if (od.TableId > 0 && !tableIds.Contains(od.TableId))
                        tableIds.Add(od.TableId);
                }
            }
            return tableIds.ToArray();
        }


        public async Task<string> MeasureNetworkLatencyAsync()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                // Check connectivity to the base URL.
                // We use HttpCompletionOption.ResponseHeadersRead to avoid downloading body if we just want latency.
                using var response = await _httpClient.GetAsync("", HttpCompletionOption.ResponseHeadersRead);
                sw.Stop();
                
                var latency = sw.ElapsedMilliseconds;
                
                // Categorize speed if needed, or just return latency
                string status = "Good";
                if (latency > 500) status = "Slow";
                if (latency > 2000) status = "Poor";
                
                return $"{latency}ms";
            }
            catch
            {
                return "Offline";
            }
        }

        //Process Refund Order
        public async Task<bool> ProcessOrderRefundAsync(int orderId, string refundMode, decimal refundAmount, string refundReason)
        {
            try
            {
                // Check if user is logged in
                var accessToken = POS_UI.Properties.Settings.Default.AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("Please log in first to process refund.");
                }
                
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = $"/api/v1/orders/{orderId}/refund";
                using var request = new HttpRequestMessage(HttpMethod.Patch, endpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }

                var requestBody = new
                {
                    refund_mode = refundMode?.ToUpperInvariant() ?? "CASH",
                    refund_amount = refundAmount,
                    refund_reason = refundReason ?? string.Empty
                };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        throw new Exception($"Authentication failed. Please log in again. Status: {response.StatusCode}\n{responseBody}");
                    }
                    throw new Exception($"Refund request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Process refund order for non-pos platform
        /// </summary>
        public async Task<bool> ProcessRefundOrderForNonPosPlatformAsync(string remoteOrderId, decimal refundAmount, string refundMode, string reason)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(remoteOrderId))
                {
                    throw new Exception("Order ID is required for refund.");
                }

                // Get Laravel bearer token
                var laravelService = new LaravelPassportService();
                var bearerToken = await laravelService.GetAccessTokenAsync();
                
                if (string.IsNullOrEmpty(bearerToken))
                {
                    throw new Exception("Failed to acquire bearer token for refund order.");
                }

                // Get tenant code from settings
                var (tenantCode, _, _) = _settingsService.LoadSettings();

                // Create HttpClient for delivery platform API
                using var http = new HttpClient();
                http.Timeout = TimeSpan.FromMinutes(5);
                http.BaseAddress = new Uri(EnvironmentService.Instance.Config.Urls.PlatformBaseUrl?.Trim() ?? "https://platform-dev.delivergate.com");
                http.DefaultRequestHeaders.Accept.Clear();
                http.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                // Add tenant code header if provided
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    http.DefaultRequestHeaders.Remove("x-tenant-code");
                    http.DefaultRequestHeaders.Add("x-tenant-code", tenantCode);
                }

                // Build endpoint URL
                var url = $"/api/v1/refund-order/{Uri.EscapeDataString(remoteOrderId)}";

                // Create form-data content
                var formData = new MultipartFormDataContent();
                formData.Add(new StringContent(refundAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)), "refund_amount");
                formData.Add(new StringContent(refundMode?.ToUpperInvariant() ?? "CASH"), "refund_mode");
                formData.Add(new StringContent(reason ?? string.Empty), "reason");

                // Send POST request
                var response = await http.PostAsync(url, formData);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Refund order request failed. Status: {response.StatusCode}\n{responseBody}");
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error processing refund order via delivery platform: {ex.Message}");
                throw;
            }
        }

        // Verify PIN for admin credentials
        public async Task<bool> VerifyPinAsync(string outletCode, string email, string pin)
        {
            try
            {
                var endpoint = "/api/v1/auth/verify-pin";
                var requestBody = new
                {
                    outlet_code = outletCode,
                    email = email,
                    pin = pin
                };
                
                var content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(endpoint, content);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (!response.IsSuccessStatusCode)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(responseBody);
                        if (doc.RootElement.TryGetProperty("message", out var messageProp))
                        {
                            throw new Exception(messageProp.GetString());
                        }
                    }
                    catch
                    {
                        // If parsing fails, use the raw response
                    }
                    throw new Exception($"PIN verification failed. Status: {response.StatusCode}\n{responseBody}");
                }

                // Parse response to check if verification was successful
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    if (doc.RootElement.TryGetProperty("data", out var dataProp))
                    {
                        if (dataProp.TryGetProperty("verified", out var verifiedProp))
                        {
                            return verifiedProp.GetBoolean();
                        }
                        // If no verified field, assume success based on status code
                        return true;
                    }
                    // If response structure is different, check status code
                    return response.IsSuccessStatusCode;
                }
                catch
                {
                    // If parsing fails, return success based on status code
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// GET menu configuration from API
        /// GET /api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal={terminalId}
        /// </summary>
        public async Task<string> GetMenuConfigAsync(int shopId, int brandId, string terminalId = "1")
        {
            try
            {
                var endpoint = $"/api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] GET {endpoint}");
                
                var response = await _httpClient.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Body: {responseBody}");
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // Return empty JSON if config doesn't exist yet
                        System.Diagnostics.Debug.WriteLine($"[ApiService] Menu config not found (404), returning empty");
                        return null;
                    }
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetMenuConfig"); } catch { }
                    }
                    throw new Exception($"Failed to get menu config. Status: {response.StatusCode}\n{responseBody}");
                }
                
                System.Diagnostics.Debug.WriteLine($"[ApiService] Menu config loaded successfully, length: {responseBody?.Length ?? 0}");
                return responseBody;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error getting menu config: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PATCH menu configuration to API
        /// PATCH /api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal={terminalId}
        /// Body: { "data": { ... menu config JSON ... } }
        /// </summary>
        public async Task<bool> SaveMenuConfigAsync(int shopId, int brandId, string menuConfigJson, string terminalId = "1")
        {
            try
            {
                var endpoint = $"/api/v1/shop/{shopId}/config/menu?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] PATCH {endpoint}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] Request Body (before wrapping): {menuConfigJson}");
                
                // Wrap the menu config in a "data" object as per API format
                var requestBody = new
                {
                    data = JsonSerializer.Deserialize<JsonElement>(menuConfigJson)
                };
                
                var wrappedJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });
                System.Diagnostics.Debug.WriteLine($"[ApiService] Request Body (after wrapping): {wrappedJson}");
                
                var content = new StringContent(
                    wrappedJson, 
                    System.Text.Encoding.UTF8, 
                    "application/json"
                );
                
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint)
                {
                    Content = content
                };
                
                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Body: {responseBody}");
                
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on SaveMenuConfig"); } catch { }
                    }
                    throw new Exception($"Failed to save menu config. Status: {response.StatusCode}\n{responseBody}");
                }
                
                System.Diagnostics.Debug.WriteLine($"[ApiService] ✓ Menu config saved successfully to API");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] ✗ Error saving menu config: {ex.Message}");
                throw;
            }
        }

        //Save Order Config
        public async Task<bool> SaveOrderConfigAsync(int shopId, int brandId, string orderConfigJson, string terminalId = "1")
        {
           try
           {
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = $"/api/v1/shop/{shopId}/config/order?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] PATCH {endpoint}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] Request Body (before wrapping): {orderConfigJson}");

                //wrap the order config in a data object as per API format
                var requestBody = new
                {
                    data = JsonSerializer.Deserialize<JsonElement>(orderConfigJson)
                };

                var wrappedJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true }); 
                System.Diagnostics.Debug.WriteLine($"[ApiService] Request Body (after wrapping): {wrappedJson}");

                var content = new StringContent(
                    wrappedJson, 
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint)
                {
                    Content = content
                };

                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if(!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try {new TokenValidationService().LogoutAndNavigateToLogin("401 on SaveOrderConfig");} catch { }
                    }
                    throw new Exception($"Failed to save order config. Status: {response.StatusCode}\n{responseBody}");
                }

                System.Diagnostics.Debug.WriteLine($"[ApiService] Order config saved successfully to API");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error saving order config: {ex.Message}");
                throw;
            }
        }

        //Get Order Config
        public async Task<string> GetOrderConfigAsync(int shopId, int brandId, string terminalId = "1")
        {
            try
            {
                var (tenantCode, _, _) = _settingsService.LoadSettings();
                var endpoint = $"/api/v1/shop/{shopId}/config/order?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] GET {endpoint}");

                var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    request.Headers.Remove("x-tenant-code");
                    request.Headers.Add("x-tenant-code", tenantCode);
                }

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Status: {response.StatusCode}");
                System.Diagnostics.Debug.WriteLine($"[ApiService] Response Body: {responseBody}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ApiService] Order config not found (404), returning null");
                        return null;
                    }
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetOrderConfig"); } catch { }
                    }
                    throw new Exception($"Failed to get order config. Status: {response.StatusCode}\n{responseBody}");
                }

                System.Diagnostics.Debug.WriteLine($"[ApiService] Order config loaded successfully, length: {responseBody?.Length ?? 0}");
                return responseBody;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error getting order config: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// GET floor plan configuration from API.
        /// GET /api/v1/shop/{shopId}/config/floor_plan?brand={brandId}&amp;terminal={terminalId}
        /// </summary>
        public async Task<string?> GetFloorPlanConfigAsync(int shopId, int brandId, string terminalId = "1")
        {
            try
            {
                var endpoint = $"/api/v1/shop/{shopId}/config/floor_plan?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] GET {endpoint}");

                var response = await _httpClient.GetAsync(endpoint);
                var responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[ApiService] Floor plan GET Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ApiService] Floor plan config not found (404)");
                        return null;
                    }

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on GetFloorPlanConfig"); } catch { }
                    }

                    throw new Exception($"Failed to get floor plan config. Status: {response.StatusCode}\n{responseBody}");
                }

                return responseBody;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error getting floor plan config: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// PATCH floor plan configuration to API.
        /// PATCH /api/v1/shop/{shopId}/config/floor_plan?brand={brandId}&amp;terminal={terminalId}
        /// Body: { "data": { ... JSON from <paramref name="floorPlanConfigJson"/> ... } }
        /// </summary>
        public async Task<bool> SaveFloorPlanConfigAsync(int shopId, int brandId, string floorPlanConfigJson, string terminalId = "1")
        {
            try
            {
                var endpoint = $"/api/v1/shop/{shopId}/config/floor_plan?brand={brandId}&terminal={terminalId}";
                System.Diagnostics.Debug.WriteLine($"[ApiService] PATCH {endpoint}");

                var requestBody = new
                {
                    data = JsonSerializer.Deserialize<JsonElement>(floorPlanConfigJson)
                };

                var wrappedJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions { WriteIndented = true });
                System.Diagnostics.Debug.WriteLine($"[ApiService] Floor plan PATCH body: {wrappedJson}");

                var content = new StringContent(
                    wrappedJson,
                    System.Text.Encoding.UTF8,
                    "application/json");

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), endpoint)
                {
                    Content = content
                };

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[ApiService] Floor plan PATCH Status: {response.StatusCode}");

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        try { new TokenValidationService().LogoutAndNavigateToLogin("401 on SaveFloorPlanConfig"); } catch { }
                    }

                    throw new Exception($"Failed to save floor plan config. Status: {response.StatusCode}\n{responseBody}");
                }

                System.Diagnostics.Debug.WriteLine($"[ApiService] Floor plan config saved successfully");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiService] Error saving floor plan config: {ex.Message}");
                throw;
            }
        }
    }
} 