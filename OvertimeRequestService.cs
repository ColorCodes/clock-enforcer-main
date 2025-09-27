using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class OvertimeRequestService
{
    private readonly HttpClient _httpClient;

    public OvertimeRequestService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<bool> SendOvertimeRequestAsync(string adUsername, string note, decimal hours)
    {
        var requestData = new
        {
            ad_username = adUsername,
            reason = note,
            overtime_hours = hours
        };

        var jsonContent = JsonSerializer.Serialize(requestData);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

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
