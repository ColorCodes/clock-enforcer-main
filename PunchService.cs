using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ClockEnforcer.Services; // Asegúrate de tener este namespace si está en otro proyecto

public class PunchService
{
    private readonly string punchUrl = "https://secure3.saashr.com/ta/rest/v1/webclock";
    private readonly AuthService authService;
    private readonly LogService logService = new LogService(); // ← Añadido

    public PunchService(AuthService authService)
    {
        this.authService = authService;
    }

    public async Task<string> PunchAsync()
    {
        string systemUsername = Environment.UserName;

        if (logService.HasExceededMaxLogins(systemUsername))
        {
            return "Maximum number of logins reached for today. Cannot punch.";
        }

        using (HttpClient client = new HttpClient())
        {
            string token = authService.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                return "No Token or Token Invalid";
            }

            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            client.DefaultRequestHeaders.Add("Accept", "*/*");
            client.DefaultRequestHeaders.Add("User-Agent", "Thunder Client");

            var payload = new { action = "punch" };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync(punchUrl, content);
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
                            logService.LogClockOut(systemUsername);
                            return $"Punch Successful: {punchType} YOU WILL BE LOGGED OUT IN 10 SECONDS";
                        } 
                        else
                        {
                            logService.LogLogin(systemUsername);
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
