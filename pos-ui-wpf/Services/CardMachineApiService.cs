using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using POS_UI.Models;

namespace POS_UI.Services
{
    public class CardMachineApiService
    {
        private static readonly HttpClient _httpClient;
        static CardMachineApiService()
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            _httpClient = new HttpClient(handler);
        }

        public class PairingRequest
        {
            [JsonPropertyName("deviceId")]
            public string DeviceId { get; set; }
            [JsonPropertyName("pairingCode")]
            public string PairingCode { get; set; }
        }

        public class PairingResponse
        {
            [JsonPropertyName("authToken")]
            public string AuthToken { get; set; }
        }

        // Transaction Request Model
        public class TransactionRequest
        {
            [JsonPropertyName("transType")]
            public string TransType { get; set; } = "SALE";
            
            [JsonPropertyName("amountTrans")]
            public int AmountTrans { get; set; }
            
            [JsonPropertyName("amountGratuity")]
            public int AmountGratuity { get; set; } = 0;
            
            [JsonPropertyName("amountCashback")]
            public int AmountCashback { get; set; } = 0;
            
            [JsonPropertyName("reference")]
            public string Reference { get; set; }
            
            [JsonPropertyName("language")]
            public string Language { get; set; } = "en_GB";
            
            [JsonPropertyName("uti")]
            public string Uti { get; set; }
        }

        // Transaction Response Models
        public class PostTransactionResponse
        {
            [JsonPropertyName("amountCashback")]
            public int AmountCashback { get; set; }
            
            [JsonPropertyName("amountGratuity")]
            public int AmountGratuity { get; set; }
            
            [JsonPropertyName("amountTrans")]
            public int AmountTrans { get; set; }
            
            [JsonPropertyName("transType")]
            public string TransType { get; set; }
            
            [JsonPropertyName("uti")]
            public string Uti { get; set; }
        }

        public class GetTransactionResponse
        {
            [JsonPropertyName("amountCashback")]
            public int? AmountCashback { get; set; }
            
            [JsonPropertyName("amountDiscount")]
            public int? AmountDiscount { get; set; }
            
            [JsonPropertyName("amountGratuity")]
            public int? AmountGratuity { get; set; }
            
            [JsonPropertyName("amountSurcharge")]
            public int? AmountSurcharge { get; set; }
            
            [JsonPropertyName("amountTrans")]
            public int? AmountTrans { get; set; }
            
            [JsonPropertyName("authorisationCode")]
            public string AuthorisationCode { get; set; }
            
            [JsonPropertyName("cardExpiryDate")]
            public string CardExpiryDate { get; set; }
            
            [JsonPropertyName("cardPan")]
            public string CardPan { get; set; }
            
            [JsonPropertyName("cardPanSequenceNumber")]
            public string CardPanSequenceNumber { get; set; }
            
            [JsonPropertyName("cardScheme")]
            public string CardScheme { get; set; }
            
            [JsonPropertyName("cardStartDate")]
            public string CardStartDate { get; set; }
            
            [JsonPropertyName("cardType")]
            public string CardType { get; set; }
            
            [JsonPropertyName("cardholderReceipt")]
            public List<string> CardholderReceipt { get; set; }
            
            [JsonPropertyName("cvmPinVerified")]
            public bool? CvmPinVerified { get; set; }
            
            [JsonPropertyName("cvmSigRequired")]
            public bool? CvmSigRequired { get; set; }
            
            [JsonPropertyName("emvAid")]
            public string EmvAid { get; set; }
            
            [JsonPropertyName("emvCryptogramType")]
            public string EmvCryptogramType { get; set; }
            
            [JsonPropertyName("emvCardholderName")]
            public string EmvCardholderName { get; set; }
            
            [JsonPropertyName("emvTsi")]
            public string EmvTsi { get; set; }
            
            [JsonPropertyName("emvTvr")]
            public string EmvTvr { get; set; }
            
            [JsonPropertyName("errorCode")]
            public string ErrorCode { get; set; }
            
            [JsonPropertyName("errorText")]
            public string ErrorText { get; set; }
            
            [JsonPropertyName("merchantId")]
            public string MerchantId { get; set; }
            
            [JsonPropertyName("merchantReceipt")]
            public List<string> MerchantReceipt { get; set; }
            
            [JsonPropertyName("merchantReference")]
            public string MerchantReference { get; set; }
            
            [JsonPropertyName("merchantTokenId")]
            public string MerchantTokenId { get; set; }
            
            [JsonPropertyName("paymentId")]
            public string PaymentId { get; set; }
            
            [JsonPropertyName("penniesAmount")]
            public int? PenniesAmount { get; set; }
            
            [JsonPropertyName("receiptNumber")]
            public int? ReceiptNumber { get; set; }
            
            [JsonPropertyName("responseCode")]
            public string ResponseCode { get; set; }
            
            [JsonPropertyName("retrievalReferenceNumber")]
            public string RetrievalReferenceNumber { get; set; }
            
            [JsonPropertyName("softwareVersion")]
            public string SoftwareVersion { get; set; }
            
            [JsonPropertyName("shortPaymentId")]
            public string ShortPaymentId { get; set; }
            
            [JsonPropertyName("stan")]
            public string Stan { get; set; }
            
            [JsonPropertyName("surchargeRate")]
            public int? SurchargeRate { get; set; }
            
            [JsonPropertyName("terminalId")]
            public string TerminalId { get; set; }
            
            [JsonPropertyName("transApproved")]
            public bool TransApproved { get; set; }
            
            [JsonPropertyName("transCancelled")]
            public bool TransCancelled { get; set; }
            
            [JsonPropertyName("transCurrencyCode")]
            public string TransCurrencyCode { get; set; }
            
            [JsonPropertyName("transDateTime")]
            public string TransDateTime { get; set; }
            
            [JsonPropertyName("transDateTimeEpoch")]
            public object TransDateTimeEpoch { get; set; } // Can be string or number
            
            [JsonPropertyName("transType")]
            public string TransType { get; set; }
            
            [JsonPropertyName("emvCryptogram")]
            public string EmvCryptogram { get; set; }
            
            [JsonPropertyName("uti")]
            public string Uti { get; set; }
            
            [JsonPropertyName("Statuses")]
            public object Statuses { get; set; } // Can be string or array
        }

        public class TransactionStatus
        {
            [JsonPropertyName("statusValue")]
            public int StatusValue { get; set; }
            
            [JsonPropertyName("statusDescription")]
            public string StatusDescription { get; set; }
        }

        private List<TransactionStatus> ParseStatuses(object statusesObj)
        {
            // For now, return empty list to avoid JSON parsing errors
            // The transaction completion detection works fine without status parsing
            return new List<TransactionStatus>();
        }

        private string GetStatusDescription(int statusValue)
        {
            return statusValue switch
            {
                0 => "Transaction started",
                1 => "Transaction Approved",
                2 => "Transaction Declined",
                3 => "Card type = MSR",
                4 => "MSR Transaction Declined",
                5 => "Card type EMV",
                6 => "Card type CTLS",
                7 => "CTLS Transaction Declined",
                8 => "Card type = manual",
                9 => "Transaction Cancelled",
                10 => "Transaction Referred",
                11 => "Transaction Finished",
                12 => "GetCard Screen Displayed",
                13 => "Manual Pan Screen Displayed",
                14 => "Pin Requested(Offline)",
                15 => "Pin Requested(Online)",
                16 => "Host Approved",
                17 => "Deferred Auth",
                18 => "Reversal Approved",
                19 => "Reversal Declined",
                20 => "Transaction Declined",
                21 => "Card User Cancelled",
                22 => "GENAC2 Failed",
                23 => "Printer General Error",
                24 => "Printer Out Of Paper",
                25 => "Amount High",
                26 => "Amount Low",
                27 => "Card Blocked",
                28 => "Card Expired",
                29 => "Card Type Not Allowed",
                30 => "Invalid Card Number",
                31 => "Pin Invalid Retry",
                32 => "Pin Invalid Last Try",
                33 => "Cashback Too High",
                34 => "Pin Cvm Required",
                35 => "Signature Cvm Required",
                36 => "Locally Declined",
                37 => "Host Declined",
                38 => "Issuer Declined",
                39 => "Issuer Unavailable",
                40 => "Update In Progress Error",
                41 => "Update Required Error",
                42 => "Reversal Not Possible Error",
                43 => "Transaction Type Not Allowed",
                44 => "Login Failed",
                45 => "Chip Unreadable",
                46 => "Chip App Unsupported Please Swipe",
                47 => "Chip Rid Unsupported Please Swipe",
                48 => "Chip Invalid Please Swipe",
                49 => "Chip not allowed Please swipe",
                50 => "Chip detected Please Insert",
                51 => "Chip detected Please Insert OR Force Fallback",
                52 => "Insert Or Swipe Card",
                53 => "Magnetic Strip Unreadable",
                54 => "Magnetic Stripe Invalid",
                55 => "Magnetic Stripe Not Allowed",
                56 => "Manual Input Invalid",
                57 => "Manual Input Invalid Length",
                58 => "Manual Input Invalid Date",
                59 => "Cashback Only Allowed Online",
                60 => "Transaction Only Allowed Online",
                61 => "Approval Code Invalid",
                62 => "Password Invalid",
                63 => "Close Batch Required",
                64 => "Close Batch Not Required",
                65 => "Technical Error",
                66 => "Hardware Error",
                _ => $"Unknown Status: {statusValue}"
            };
        }

        public async Task<string> PairDeviceAsync(string ip, string port, string apiEndpoint, string deviceId, string pairingCode)
        {
            string url = $"https://{ip}:{port}{apiEndpoint}/pair?tid={deviceId}&pairingCode={pairingCode}";
            
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Pairing failed: {response.StatusCode}\n{error}");
                }
                var responseJson = await response.Content.ReadAsStringAsync();
                var pairingResponse = JsonSerializer.Deserialize<PairingResponse>(responseJson);
                if (pairingResponse == null || string.IsNullOrEmpty(pairingResponse.AuthToken))
                    throw new Exception("Pairing succeeded but no auth token returned.");
                return pairingResponse.AuthToken;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error pairing with card machine: {ex.Message}");
            }
        }

        // New method to process card payment transaction
        public async Task<CardTransactionResult> ProcessCardPaymentAsync(CardMachineModel cardMachine, decimal amount, string reference, Action<string> statusUpdateCallback = null)
        {
            try
            {
                // Step 1: Initiate transaction with quick timeout for connection issues
                var transactionRequest = new TransactionRequest
                {
                    TransType = "SALE",
                    AmountTrans = (int)(amount * 100), // Convert to minor units (pennies)
                    AmountGratuity = 0,
                    AmountCashback = 0,
                    Reference = reference,
                    Language = "en_GB"
                };

                string transactionUrl = $"https://{cardMachine.IPAddress}:{cardMachine.Port}{cardMachine.APIEndpoint}/transaction?tid={cardMachine.DeviceId}";
                
                var json = JsonSerializer.Serialize(transactionRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, transactionUrl);
                request.Content = content;
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cardMachine.AuthToken);

                // Use a shorter timeout for the initial request to detect connection issues quickly
                using (var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                    var responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        // Handle specific error cases
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            throw new Exception("Card machine authentication failed. Token may be expired. Please re-pair the device.");
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            throw new Exception("Card machine not found or not reachable. Please check device connection.");
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                        {
                            throw new Exception("Card machine connection timeout. Device may be offline or unreachable.");
                        }
                        else
                        {
                            throw new Exception($"Transaction initiation failed: {response.StatusCode}\n{responseJson}");
                        }
                    }

                    // Handle potential non-JSON responses for initial request
                    if (string.IsNullOrWhiteSpace(responseJson))
                    {
                        throw new Exception("Card machine returned empty response. Please check device status.");
                    }

                    if (!responseJson.TrimStart().StartsWith("{"))
                    {
                        // Non-JSON response - might be an error message or status
                        throw new Exception($"Card machine returned non-JSON response: {responseJson.Trim()}");
                    }

                    PostTransactionResponse postResponse;
                    try
                    {
                        postResponse = JsonSerializer.Deserialize<PostTransactionResponse>(responseJson);
                    }
                    catch (JsonException jsonEx)
                    {
                        throw new Exception($"Failed to parse card machine response: {jsonEx.Message}. Response: {responseJson}");
                    }

                    if (postResponse == null || string.IsNullOrEmpty(postResponse.Uti))
                    {
                        throw new Exception("Transaction initiated but no UTI returned.");
                    }

                    // Step 2: Poll for transaction result with status updates
                    return await PollTransactionResultAsync(cardMachine, postResponse.Uti, statusUpdateCallback);
                }
            }
            catch (System.OperationCanceledException)
            {
                return new CardTransactionResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Card machine connection timeout. Device may be offline or unreachable."
                };
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return new CardTransactionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Card machine connection error: {ex.Message}. Please check device connection and network."
                };
            }
            catch (Exception ex)
            {
                return new CardTransactionResult
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // New method to process card refund transaction (same as SALE but with REFUND type)
        public async Task<CardTransactionResult> ProcessCardRefundAsync(CardMachineModel cardMachine, decimal amount, string reference, Action<string> statusUpdateCallback = null)
        {
            try
            {
                var transactionRequest = new TransactionRequest
                {
                    TransType = "REFUND",
                    AmountTrans = (int)(amount * 100),
                    AmountGratuity = 0,
                    AmountCashback = 0,
                    Reference = reference,
                    Language = "en_GB"
                };

                string transactionUrl = $"https://{cardMachine.IPAddress}:{cardMachine.Port}{cardMachine.APIEndpoint}/transaction?tid={cardMachine.DeviceId}";

                var json = JsonSerializer.Serialize(transactionRequest);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, transactionUrl);
                request.Content = content;
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cardMachine.AuthToken);

                using (var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                {
                    var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                    var responseJson = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            throw new Exception("Card machine authentication failed. Token may be expired. Please re-pair the device.");
                        else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                            throw new Exception("Card machine not found or not reachable. Please check device connection.");
                        else if (response.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                            throw new Exception("Card machine connection timeout. Device may be offline or unreachable.");
                        else
                            throw new Exception($"Refund initiation failed: {response.StatusCode}\n{responseJson}");
                    }

                    if (string.IsNullOrWhiteSpace(responseJson) || !responseJson.TrimStart().StartsWith("{"))
                    {
                        throw new Exception($"Card machine returned unexpected response: {responseJson?.Trim()}");
                    }

                    var postResponse = JsonSerializer.Deserialize<PostTransactionResponse>(responseJson);
                    if (postResponse == null || string.IsNullOrEmpty(postResponse.Uti))
                    {
                        throw new Exception("Refund initiated but no UTI returned.");
                    }

                    return await PollTransactionResultAsync(cardMachine, postResponse.Uti, statusUpdateCallback);
                }
            }
            catch (System.OperationCanceledException)
            {
                return new CardTransactionResult { IsSuccess = false, ErrorMessage = "Card machine connection timeout. Device may be offline or unreachable." };
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return new CardTransactionResult { IsSuccess = false, ErrorMessage = $"Card machine connection error: {ex.Message}. Please check device connection and network." };
            }
            catch (Exception ex)
            {
                return new CardTransactionResult { IsSuccess = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<CardTransactionResult> PollTransactionResultAsync(CardMachineModel cardMachine, string uti, Action<string> statusUpdateCallback = null)
        {
            int maxAttempts = 120; // ~2 minutes total wait time
            int attempt = 0;
            string lastStatus = "";

            while (attempt < maxAttempts)
            {
                try
                {
                    string pollUrl = $"https://{cardMachine.IPAddress}:{cardMachine.Port}{cardMachine.APIEndpoint}/transaction?tid={cardMachine.DeviceId}&uti={uti}";
                    
                    var request = new HttpRequestMessage(HttpMethod.Get, pollUrl);
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cardMachine.AuthToken);

                    // Use shorter timeout for polling requests
                    using (var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                        var responseJson = await response.Content.ReadAsStringAsync();

                        if (response.IsSuccessStatusCode)
                        {
                            // Debug: Log the actual response
            
                            
                            // Handle potential non-JSON responses
                            if (string.IsNullOrWhiteSpace(responseJson))
                            {
                                // Empty response - continue polling
                                await Task.Delay(1000);
                                attempt++;
                                continue;
                            }

                            // Check if response starts with non-JSON content
                            if (!responseJson.TrimStart().StartsWith("{"))
                            {
                                // Non-JSON response - might be a status message
                                if (responseJson.Trim().StartsWith("T", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Likely a transaction status message
                                    statusUpdateCallback?.Invoke(responseJson.Trim());
                                }
                                else
                                {
                                    // Show any non-JSON response as status
                                    statusUpdateCallback?.Invoke(responseJson.Trim());
                                }
                                
                                // Continue polling for JSON response
                                await Task.Delay(1000);
                                attempt++;
                                continue;
                            }

                            try
                            {
                                var transactionResponse = JsonSerializer.Deserialize<GetTransactionResponse>(responseJson);
                                if (transactionResponse != null)
                                {
                                    // Debug: Log the parsed response
                                    var statuses = ParseStatuses(transactionResponse.Statuses);
                    
                                    
                                    // Handle status updates
                                    if (statuses != null && statuses.Count > 0)
                                    {
                                        var latestStatus = statuses.Last();
                                        var statusDescription = GetStatusDescription(latestStatus.StatusValue);
                                        
                                        // Only update if status changed
                                        if (statusDescription != lastStatus)
                                        {
                                            lastStatus = statusDescription;
                                            statusUpdateCallback?.Invoke(statusDescription);
                                        }
                                    }

                                    // Check if transaction is complete
                                    if (transactionResponse.TransApproved || transactionResponse.TransCancelled || !string.IsNullOrEmpty(transactionResponse.ErrorCode))
                                    {
                                        return new CardTransactionResult
                                        {
                                            IsSuccess = transactionResponse.TransApproved,
                                            IsCancelled = transactionResponse.TransCancelled,
                                            AuthorisationCode = transactionResponse.AuthorisationCode,
                                            RetrievalReferenceNumber = transactionResponse.RetrievalReferenceNumber,
                                            CardPan = transactionResponse.CardPan,
                                            CardScheme = transactionResponse.CardScheme,
                                            ResponseCode = transactionResponse.ResponseCode,
                                            ErrorMessage = transactionResponse.ErrorText,
                                            Uti = uti
                                        };
                                    }
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                // JSON parsing failed - log and continue polling
            
                                
                                // If it looks like a status message, show it
                                if (!string.IsNullOrWhiteSpace(responseJson))
                                {
                                    statusUpdateCallback?.Invoke($"Status: {responseJson.Trim()}");
                                }
                                
                                // Continue polling
                                await Task.Delay(1000);
                                attempt++;
                                continue;
                            }
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            // Token expired during transaction
                            return new CardTransactionResult
                            {
                                IsSuccess = false,
                                ErrorMessage = "Card machine authentication failed during transaction. Token may have expired."
                            };
                        }
                    }

                    // Wait 1 second before next poll
                    await Task.Delay(1000);
                    attempt++;
                }
                catch (System.OperationCanceledException)
                {
                    // Timeout on individual poll request - continue polling
                    await Task.Delay(1000);
                    attempt++;
                }
                catch (System.Net.Http.HttpRequestException)
                {
                    // Connection error during polling - continue polling
                    await Task.Delay(1000);
                    attempt++;
                }
                catch (Exception ex)
                {
                    // Other errors - continue polling
                    await Task.Delay(1000);
                    attempt++;
                }
            }

            return new CardTransactionResult
            {
                IsSuccess = false,
                ErrorMessage = "Transaction timeout - no response received within 2 minutes. This may be due to receipt printing or slow network response."
            };
        }

        public async Task<bool> CreateUserAsync(string ip, string port, string apiEndpoint, string terminalId, string userId, string userName, string password, bool supervisor, string authToken = null)
        {
            string url = $"https://{ip}:{port}{apiEndpoint}/user?userId={userId}&userName={userName}&password={password}&supervisor={supervisor}&tid={terminalId}";
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating user: {url}");
                
                // Create a new request message to add headers
                var request = new HttpRequestMessage(HttpMethod.Post, url);
                
                // Add Authorization header if auth token is provided
                if (!string.IsNullOrEmpty(authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                    System.Diagnostics.Debug.WriteLine($"Adding Authorization header: Bearer {authToken}");
                }
                
                var response = await _httpClient.SendAsync(request);
                System.Diagnostics.Debug.WriteLine($"Create user response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Create user error response: {error}");
                    return false;
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Create user success response: {responseJson}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Create user exception: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string ip, string port, string apiEndpoint, string terminalId, string userId, string authToken = null)
        {
            string url = $"https://{ip}:{port}{apiEndpoint}/user?userId={userId}&tid={terminalId}";
            
            try
            {
                System.Diagnostics.Debug.WriteLine($"Deleting user: {url}");
                
                // Create a new request message to add headers
                var request = new HttpRequestMessage(HttpMethod.Delete, url);
                
                // Add Authorization header if auth token is provided
                if (!string.IsNullOrEmpty(authToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
                    System.Diagnostics.Debug.WriteLine($"Adding Authorization header: Bearer {authToken}");
                }
                
                var response = await _httpClient.SendAsync(request);
                System.Diagnostics.Debug.WriteLine($"Delete user response status: {response.StatusCode}");
                
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Delete user error response: {error}");
                    return false;
                }
                
                var responseJson = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"Delete user success response: {responseJson}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Delete user exception: {ex.Message}");
                return false;
            }
        }

        // Card terminal Z Report (reports?reportType=zReport)
        public async Task<bool> PrintZReportAsync(CardMachineModel cardMachine)
        {
            string url = $"https://{cardMachine.IPAddress}:{cardMachine.Port}{cardMachine.APIEndpoint}/reports?reportType=zReport&tid={cardMachine.DeviceId}&disablePrinting=false";
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cardMachine.AuthToken);
                using (var timeoutCts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(20)))
                {
                    var response = await _httpClient.SendAsync(request, timeoutCts.Token);
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Z Report failed: {response.StatusCode}\n{body}");
                    }
                    // Parse response and print a receipt summary from our printer
                    try
                    {
                        var data = System.Text.Json.JsonSerializer.Deserialize<POS_UI.Models.CardMachineZReportResponse>(body);
                        if (data != null)
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine("**CARD Z REPORT**");
                            sb.AppendLine(new string('=', 50));
                            sb.AppendLine($"Report Type: {data.reportType}");
                            sb.AppendLine(new string('-', 50));
                            var currency = POS_UI.Services.GlobalDataService.Instance?.ShopDetails?.Currency ?? "£";
                            sb.AppendLine($"Sales:|**{currency} {(data.saleAmount/100.0m):F2} (Count: {data.saleCount})**");
                            sb.AppendLine($"Refunds:|**{currency} {(data.refundAmount/100.0m):F2} (Count: {data.refundCount})**");
                            sb.AppendLine($"Tips:|**{currency} {(data.gratuityAmount/100.0m):F2}** (Count: {data.gratuityCount})");
                            sb.AppendLine($"Cashback:|**{currency} {(data.cashbackAmount/100.0m):F2}** (Count: {data.cashbackCount})");
                            sb.AppendLine($"Pennies:|**{currency} {(data.penniesAmount/100.0m):F2}** (Count: {data.penniesCount})");
                            sb.AppendLine(new string('-', 50));
                            var total = (data.saleAmount - data.refundAmount) / 100.0m;
                            sb.AppendLine($"**NET TOTAL:**|**{currency} {total:F2}**");
                            try
                            {
                                var printers = POS_UI.Services.ReceiptPrintingService.Instance.GetActivePrinters();
                                foreach (var p in printers)
                                {
                                    await POS_UI.Services.ReceiptPrintingService.Instance.PrintRawContentAsync(p, sb.ToString());
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    // Result class for card transactions
    public class CardTransactionResult
    {
        /// <summary>When true, split/checkout flow has already shown a dialog for this failure — do not show a second generic error.</summary>
        public bool UserAlreadyNotifiedOfFailure { get; set; }

        public bool IsSuccess { get; set; }
        public bool IsCancelled { get; set; }
        public string AuthorisationCode { get; set; }
        public string RetrievalReferenceNumber { get; set; }
        public string CardPan { get; set; }
        public string CardScheme { get; set; }
        public string ResponseCode { get; set; }
        public string ErrorMessage { get; set; }
        public string Uti { get; set; }
    }
} 