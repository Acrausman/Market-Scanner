using MarketScanner.Data.Diagnostics;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace MarketScanner.Data.Services
{
    public class SmsService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;

        public SmsService(string apiKey = "textbelt") // default: free test key
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
        }

        public async Task SendSmsAsync(string to, string message)
        {
            try
            {
                Logger.Info($"[SMS] Sending via Textbelt to {to}: {message}");
                Debug.WriteLine($"[SMS] Sending via Textbelt to {to}: {message}");

                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "phone", to },
                    { "message", message },
                    { "key", _apiKey }
                });

                var response = await _httpClient.PostAsync("https://textbelt.com/text", content);
                var result = await response.Content.ReadAsStringAsync();

                Logger.Info($"[SMS Response] {result}");
                Debug.WriteLine($"[SMS Response] {result}");
            }
            catch (Exception ex)
            {
                Logger.Error($"[SMS Error] {ex.Message}");
                Debug.WriteLine($"[SMS Error] {ex.Message}");
            }
        }
    }
}
