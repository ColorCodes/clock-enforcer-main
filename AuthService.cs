// AuthService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using ClockEnforcer.Services;

namespace ClockEnforcer.Services
{
    public class AuthService
    {
        private readonly string apiKey;
        private readonly string loginUrl = "http://secure2.saashr.com/ta/rest/v1/login";
        private string authToken;

        public AuthService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey), "API key no puede estar vacía");
            this.apiKey = apiKey;
        }

        public async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Thunder Client (https://www.thunderclient.com)");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Api-Key", apiKey);

            var payload = new
            {
                credentials = new { username, password, company = "AGI04" }
            };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(loginUrl, content);
                var respString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(respString);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<LoginResponse>(respString);
                    authToken = result.Token;
                    return true;
                }

                Console.WriteLine($"Error: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                return false;
            }
        }

        public string GetToken() => authToken;
    }

    public class LoginResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; }
    }
}

