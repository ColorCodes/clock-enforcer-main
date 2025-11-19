using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ClockEnforcer.Services
{
    public class OvertimeRequestService
    {
        private readonly HttpClient httpClient;
        private const string BaseUrl = "https://clock.adrianas.com/backend/timeclock";

        public OvertimeRequestService()
        {
            httpClient = new HttpClient();
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
            using var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await httpClient.PostAsync($"{BaseUrl}/overtime", content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<OvertimeTodayResponse?> GetTodayOvertimeStatusAsync(string adUsername)
        {
            try
            {
                var response = await httpClient.GetAsync($"{BaseUrl}/overtime/today?ad_username={adUsername}");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<OvertimeTodayResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }
    }

    public class OvertimeTodayResponse
    {
        public int Status { get; set; }
        public string Label { get; set; }
        public OvertimeData Data { get; set; }
    }

    public class OvertimeData
    {
        public bool Accepted { get; set; }
        public decimal Hours { get; set; }
    }
}
