using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClockEnforcer.Services
{
    /// <summary>
    /// Handles authentication against the SaaShr endpoint and stores the bearer token
    /// for subsequent API calls.
    /// </summary>
    public sealed class AuthService
    {
        private readonly string _apiKey;
        private const string LoginUrl = "http://secure2.saashr.com/ta/rest/v1/login";
        private string _authToken;

        public AuthService(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentNullException(nameof(apiKey), "API key no puede estar vacía");

            _apiKey = apiKey;
        }

        /// <summary>
        /// Authenticates the provided credentials and caches the bearer token when successful.
        /// </summary>
        public async Task<bool> AuthenticateUserAsync(string username, string password)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "Thunder Client (https://www.thunderclient.com)");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Api-Key", _apiKey);

            var payload = new
            {
                credentials = new { username, password, company = "AGI04" }
            };

            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(LoginUrl, content);
                var respString = await response.Content.ReadAsStringAsync();
                Console.WriteLine(respString);

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<LoginResponse>(respString);
                    _authToken = result?.Token;
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

        public string GetToken() => _authToken;

        private sealed class LoginResponse
        {
            [JsonPropertyName("token")]
            public string Token { get; set; }
        }
    }

    /// <summary>
    /// Centralises all file-based audit logging, credential caching and punch state helpers.
    /// </summary>
    public sealed class LogService
    {
        private static readonly string BaseFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ClockEnforcer");

        private static readonly string LoginLogFilePath =
            Path.Combine(BaseFolder, "user_logins.txt");

        private static readonly string CredentialsFilePath =
            Path.Combine(BaseFolder, "user_credentials.txt");

        static LogService()
        {
            if (!Directory.Exists(BaseFolder))
                Directory.CreateDirectory(BaseFolder);

            if (!File.Exists(LoginLogFilePath))
                File.Create(LoginLogFilePath).Close();

            if (!File.Exists(CredentialsFilePath))
                File.Create(CredentialsFilePath).Close();
        }

        /// <summary>
        /// Records a punch-in event and, if this is the first login today, stores the credentials.
        /// </summary>
        public void LogLogin(string clockinUsername, string password = null)
        {
            string systemUsername = Environment.UserName;
            string timestamp = DateTime.Now.ToString();

            var today = DateTime.Today;
            bool alreadyLoggedInToday = File.ReadAllLines(LoginLogFilePath)
                .Any(line => line.StartsWith(systemUsername) && line.Contains("LOGIN") &&
                             DateTime.TryParse(line.Split(',')[2], out var dt) && dt.Date == today);

            File.AppendAllText(LoginLogFilePath, $"{systemUsername},LOGIN,{timestamp}{Environment.NewLine}");

            if (!alreadyLoggedInToday && password != null)
            {
                File.AppendAllText(CredentialsFilePath,
                    $"{systemUsername},{timestamp},{clockinUsername},{password}{Environment.NewLine}");
                File.AppendAllText(Path.Combine(BaseFolder, "overtime_debug_log.txt"),
                    $"{timestamp}: Credenciales guardadas de {systemUsername} como {clockinUsername}{Environment.NewLine}");
            }
        }

        /// <summary>
        /// Records a punch-out event for the specified or current user.
        /// </summary>
        public void LogClockOut(string username = null)
        {
            string systemUsername = username ?? Environment.UserName;
            string timestamp = DateTime.Now.ToString();

            File.AppendAllText(LoginLogFilePath, $"{systemUsername},CLOCKOUT,{timestamp}{Environment.NewLine}");
        }

        public int GetTodayLoginCount(string username)
        {
            if (!File.Exists(LoginLogFilePath)) return 0;

            return File.ReadAllLines(LoginLogFilePath)
                .Where(line => line.StartsWith(username) && line.Contains("LOGIN"))
                .Select(line => line.Split(','))
                .Count(parts => parts.Length >= 3 && DateTime.TryParse(parts[2], out var dt) && dt.Date == DateTime.Today);
        }

        public bool IsUserLockedOut(string username)
        {
            if (!File.Exists(LoginLogFilePath))
                return false;

            var lastClockOut = File.ReadAllLines(LoginLogFilePath)
                .Where(line => line.StartsWith(username + ",CLOCKOUT"))
                .Select(line => DateTime.Parse(line.Split(',')[2]))
                .LastOrDefault();

            if (lastClockOut == default)
                return false;

            TimeSpan window = lastClockOut.TimeOfDay is var t && t >= TimeSpan.FromHours(10) && t <= TimeSpan.FromHours(16)
                ? TimeSpan.FromMinutes(50)
                : TimeSpan.FromHours(4);

            return DateTime.Now < lastClockOut.Add(window);
        }

        public (string user, string pass) GetLastSavedCredentials(string systemUsername)
        {
            if (!File.Exists(CredentialsFilePath))
                return (null, null);

            var lines = File.ReadAllLines(CredentialsFilePath);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var parts = lines[i].Split(',');
                if (parts.Length == 4 && parts[0] == systemUsername)
                {
                    return (parts[2], parts[3]);
                }
            }

            return (null, null);
        }

        public bool HasExceededMaxLogins(string username) => GetTodayLoginCount(username) >= 4;

        public bool ShouldNextPunchBeOut(string username) => GetTodayLoginCount(username) % 2 == 1;
    }

    /// <summary>
    /// Sends clock punches using the bearer token supplied by <see cref="AuthService"/>
    /// while enforcing logging rules through <see cref="LogService"/>.
    /// </summary>
    public sealed class PunchService
    {
        private const string PunchUrl = "https://secure3.saashr.com/ta/rest/v1/webclock";

        private readonly AuthService _authService;
        private readonly LogService _logService;

        public PunchService(AuthService authService, LogService logService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Executes a punch action and mirrors the original textual responses.
        /// </summary>
        public async Task<string> PunchAsync()
        {
            string systemUsername = Environment.UserName;

            if (_logService.HasExceededMaxLogins(systemUsername))
            {
                return "Maximum number of logins reached for today. Cannot punch.";
            }

            using (HttpClient client = new HttpClient())
            {
                string token = _authService.GetToken();
                if (string.IsNullOrEmpty(token))
                {
                    return "No Token or Token Invalid";
                }

                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                client.DefaultRequestHeaders.Add("Accept", "*/*");
                client.DefaultRequestHeaders.Add("User-Agent", "Thunder Client");

                var payload = new { action = "punch" };
                using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await client.PostAsync(PunchUrl, content);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        JsonElement root = doc.RootElement;

                        if (root.TryGetProperty("punch", out JsonElement punchElement) &&
                            punchElement.TryGetProperty("type", out JsonElement typeElement))
                        {
                            string punchType = typeElement.GetString() ?? "UNKNOWN";

                            if (punchType.Equals("PUNCH_OUT", StringComparison.OrdinalIgnoreCase))
                            {
                                _logService.LogClockOut(systemUsername);
                                return $"Punch Successful: {punchType} YOU WILL BE LOGGED OUT IN 10 SECONDS";
                            }
                            else
                            {
                                _logService.LogLogin(systemUsername);
                                return $"Punch Successful: {punchType}";
                            }
                        }
                        else
                        {
                            return $"Punch Not Successful: Invalid Punch Time Detected. {jsonResponse}";
                        }
                    }
                }

                return "Punch Failed!";
            }
        }
    }

    /// <summary>
    /// Wraps the overtime request HTTP endpoint for the user interface.
    /// </summary>
    public sealed class OvertimeRequestService
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public async Task<bool> SendOvertimeRequestAsync(string adUsername, string note, decimal hours)
        {
            var requestData = new
            {
                ad_username = adUsername,
                reason = note,
                overtime_hours = hours
            };

            var jsonContent = JsonSerializer.Serialize(requestData);
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("https://clock.adrianas.com/backend/timeclock/overtime", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

namespace ClockEnforcer
{
    /// <summary>
    /// Stores constants shared across the application without leaking them through the UI layer.
    /// </summary>
    internal static class SaashrConfig
    {
        public const string ApiKey = "iibh7b86dlces64stxqwm15n65kvkhf3";
    }
}
