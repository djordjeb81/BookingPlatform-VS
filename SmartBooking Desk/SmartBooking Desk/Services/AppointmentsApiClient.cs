using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartBooking_Desk.Infrastructure;

namespace SmartBooking_Desk.Services
{
    public class AppointmentsApiClient
    {
        private readonly HttpClient _httpClient;

        public AppointmentsApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BackendSettings.BaseUrl)
            };
        }

        public async Task<string> GetAppointmentsRawAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/Appointments", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            response.EnsureSuccessStatusCode();
            return content;
        }

        public async Task<string> GetInboxRawAsync(CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync("api/Appointments/inbox", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            response.EnsureSuccessStatusCode();
            return content;
        }
    }
}