using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartBooking_Desk.Infrastructure;
using SmartBooking_Desk.Models.Licensing;

namespace SmartBooking_Desk.Services.Licensing
{
    public class LicenseApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public LicenseApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BackendSettings.BaseUrl)
            };
        }

        public void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<RegisterDeviceResponseDto> RegisterDeviceAsync(
            RegisterDeviceRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/License/register-device", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Registracija uređaja nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<RegisterDeviceResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za registraciju uređaja nije ispravan.");

            return result;
        }

        public async Task<RefreshLicenseResponseDto> RefreshAsync(
            RefreshLicenseRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/License/refresh", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Osvežavanje licence nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<RefreshLicenseResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za osvežavanje licence nije ispravan.");

            return result;
        }

        public async Task<LicenseStatusResponseDto> GetStatusAsync(
            string hwidHash,
            CancellationToken cancellationToken = default)
        {
            var url = $"api/License/status?hwidHash={Uri.EscapeDataString(hwidHash)}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Provera statusa licence nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<LicenseStatusResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za status licence nije ispravan.");

            return result;
        }
    }
}