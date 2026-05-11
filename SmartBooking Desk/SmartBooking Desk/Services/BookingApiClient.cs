using SmartBooking_Desk.Infrastructure;
using SmartBooking_Desk.Models.Appointments;
using SmartBooking_Desk.Models.Auth;
using SmartBooking_Desk.Models.BusinessCustomers;
using SmartBooking_Desk.Models.Businesses;
using SmartBooking_Desk.Models.Resources;
using SmartBooking_Desk.Models.Scheduling;
using SmartBooking_Desk.Models.Services;
using SmartBooking_Desk.Models.Staff;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;


namespace SmartBooking_Desk.Services
{
    public class BookingApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public BookingApiClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BackendSettings.BaseUrl)
            };
        }

        public async Task<AuthResponseDto> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            var body = new LoginRequestDto
            {
                Email = email,
                Password = password
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Auth/login", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Prijava nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AuthResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za prijavu nije ispravan.");

            return result;
        }

        public async Task<AuthResponseDto> RegisterAsync(
            string email,
            string password,
            string fullName,
            long? initialBusinessId = null,
            CancellationToken cancellationToken = default)
        {
            var body = new RegisterRequestDto
            {
                Email = email,
                Password = password,
                FullName = fullName,
                InitialBusinessId = initialBusinessId
            };

            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Auth/register", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje naloga nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AuthResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje naloga nije ispravan.");

            return result;
        }

        public async Task<AuthResponseDto> RegisterOwnerAsync(
            RegisterOwnerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Auth/register-owner", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje naloga i biznisa nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AuthResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje naloga i biznisa nije ispravan.");

            return result;
        }

        public void SetBearerToken(string token)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<List<AppointmentListItemDto>> GetAppointmentsAsync(long? businessId = null, CancellationToken cancellationToken = default)
        {
            var url = businessId.HasValue
                ? $"api/Appointments?businessId={businessId.Value}"
                : "api/Appointments";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<AppointmentListItemDto>>(responseText, _jsonOptions);
            return result ?? new List<AppointmentListItemDto>();
        }

        public async Task<List<AppointmentInboxItemDto>> GetInboxAsync(long? businessId = null, CancellationToken cancellationToken = default)
        {
            var url = businessId.HasValue
                ? $"api/Appointments/inbox?businessId={businessId.Value}"
                : "api/Appointments/inbox";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje obaveštenja nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<AppointmentInboxItemDto>>(responseText, _jsonOptions);
            return result ?? new List<AppointmentInboxItemDto>();
        }

        public async Task<List<ServiceItemDto>> GetServicesAsync(long businessId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/Services?businessId={businessId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje usluga nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ServiceItemDto>>(responseText, _jsonOptions);
            return result ?? new List<ServiceItemDto>();
        }

        public async Task<List<ServiceStepItemDto>> GetServiceStepsAsync(long serviceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/ServiceSteps?serviceId={serviceId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje koraka usluge nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ServiceStepItemDto>>(responseText, _jsonOptions);
            return result ?? new List<ServiceStepItemDto>();
        }

        public async Task<ServiceItemDto> CreateServiceAsync(CreateServiceRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Services", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje usluge nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje usluge nije ispravan.");

            return result;
        }
        public async Task<ServiceItemDto> UpdateServiceAsync(long serviceId, UpdateServiceRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/Services/{serviceId}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena usluge nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu usluge nije ispravan.");

            return result;
        }

        public async Task ActivateServiceAsync(long serviceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Services/{serviceId}/activate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Aktivacija usluge nije uspela. Server vraća: {responseText}");
        }

        public async Task DeactivateServiceAsync(long serviceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Services/{serviceId}/deactivate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Deaktivacija usluge nije uspela. Server vraća: {responseText}");
        }

        public async Task DeleteServiceAsync(long serviceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"api/Services/{serviceId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje usluge nije uspelo. Server vraća: {responseText}");
        }

        public async Task<List<StaffItemDto>> GetStaffAsync(long businessId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/Staff?businessId={businessId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje radnika nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<StaffItemDto>>(responseText, _jsonOptions);
            return result ?? new List<StaffItemDto>();
        }

        public async Task<StaffItemDto> CreateStaffAsync(CreateStaffRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Staff", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje radnika nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<StaffItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje radnika nije ispravan.");

            return result;
        }

        public async Task<StaffItemDto> UpdateStaffAsync(long staffId, UpdateStaffRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/Staff/{staffId}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena radnika nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<StaffItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu radnika nije ispravan.");

            return result;
        }

        public async Task<List<StaffScheduleRuleDto>> GetStaffScheduleRulesAsync(long staffId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/StaffSchedules?staffMemberId={staffId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje rasporeda radnika nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/StaffSchedules?staffMemberId={staffId}. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<StaffScheduleRuleDto>>(responseText, _jsonOptions);
            return result ?? new List<StaffScheduleRuleDto>();
        }

        public async Task<List<StaffScheduleRuleDto>> ReplaceStaffScheduleRulesAsync(
            ReplaceStaffScheduleRulesRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/StaffSchedules/replace", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Čuvanje rasporeda radnika nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/StaffSchedules/replace. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<StaffScheduleRuleDto>>(responseText, _jsonOptions);
            return result ?? new List<StaffScheduleRuleDto>();
        }

        public async Task ActivateStaffAsync(long staffId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Staff/{staffId}/activate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Aktivacija radnika nije uspela. Server vraća: {responseText}");
        }

        public async Task DeactivateStaffAsync(long staffId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Staff/{staffId}/deactivate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Deaktivacija radnika nije uspela. Server vraća: {responseText}");
        }

        public async Task DeleteStaffAsync(long staffId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"api/Staff/{staffId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje radnika nije uspelo. Server vraća: {responseText}");
        }

        public async Task<List<ResourceItemDto>> GetResourcesAsync(long businessId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/Resources?businessId={businessId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje resursa nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ResourceItemDto>>(responseText, _jsonOptions);
            return result ?? new List<ResourceItemDto>();
        }

        public async Task<List<ServiceResourceUsageDto>> GetServiceResourceUsagesAsync(
            long serviceId,
            CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(
                $"api/ServiceResourceUsages?serviceId={serviceId}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje resursa za uslugu nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/ServiceResourceUsages?serviceId={serviceId}. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ServiceResourceUsageDto>>(responseText, _jsonOptions);
            return result ?? new List<ServiceResourceUsageDto>();
        }

        public async Task<ServiceResourceUsageDto> CreateServiceResourceUsageAsync(
            CreateServiceResourceUsageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/ServiceResourceUsages", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Dodavanje resursa na uslugu nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceResourceUsageDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za dodavanje resursa na uslugu nije ispravan.");

            return result;
        }

        public async Task<ServiceResourceUsageDto> UpdateServiceResourceUsageAsync(
            long id,
            UpdateServiceResourceUsageRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/ServiceResourceUsages/{id}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena resursa usluge nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceResourceUsageDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu resursa usluge nije ispravan.");

            return result;
        }

        public async Task DeleteServiceResourceUsageAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync(
                $"api/ServiceResourceUsages/{id}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje resursa sa usluge nije uspelo. Server vraća: {responseText}");
        }

        public async Task<List<ServiceStepResourceRequirementDto>> GetServiceStepResourceRequirementsAsync(
    long serviceStepId,
    CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(
                $"api/ServiceStepResourceRequirements?serviceStepId={serviceStepId}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje resursa za korak usluge nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ServiceStepResourceRequirementDto>>(responseText, _jsonOptions);
            return result ?? new List<ServiceStepResourceRequirementDto>();
        }

        public async Task<ServiceStepResourceRequirementDto> CreateServiceStepResourceRequirementAsync(
    CreateServiceStepResourceRequirementRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/ServiceStepResourceRequirements", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Dodavanje resursa na korak usluge nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceStepResourceRequirementDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za dodavanje resursa na korak usluge nije ispravan.");

            return result;
        }

        public async Task<ServiceStepResourceRequirementDto> UpdateServiceStepResourceRequirementAsync(
    long id,
    UpdateServiceStepResourceRequirementRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/ServiceStepResourceRequirements/{id}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena resursa za korak usluge nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceStepResourceRequirementDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu resursa koraka usluge nije ispravan.");

            return result;
        }

        public async Task DeleteServiceStepResourceRequirementAsync(
    long id,
    CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync(
                $"api/ServiceStepResourceRequirements/{id}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje resursa sa koraka usluge nije uspelo. Server vraća: {responseText}");
        }

        public async Task DeleteServiceResourceRequirementAsync(
    long id,
    CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync(
                $"api/ServiceResourceRequirements/{id}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje veze usluge i resursa nije uspelo. Server vraća: {responseText}");
        }

        public async Task<ServiceResourceRequirementDto> UpdateServiceResourceRequirementAsync(
    long id,
    UpdateServiceResourceRequirementRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/ServiceResourceRequirements/{id}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena veze usluge i resursa nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceResourceRequirementDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu veze usluge i resursa nije ispravan.");

            return result;
        }

        public async Task<ServiceResourceRequirementDto> CreateServiceResourceRequirementAsync(
    CreateServiceResourceRequirementRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/ServiceResourceRequirements", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Povezivanje resursa sa uslugom nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ServiceResourceRequirementDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za povezivanje resursa sa uslugom nije ispravan.");

            return result;
        }



        public async Task<List<ServiceResourceRequirementDto>> GetServiceResourceRequirementsAsync(
    long serviceId,
    CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync(
                $"api/ServiceResourceRequirements?serviceId={serviceId}",
                cancellationToken);

            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje resursa za uslugu nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<ServiceResourceRequirementDto>>(responseText, _jsonOptions);
            return result ?? new List<ServiceResourceRequirementDto>();
        }

        public async Task<ResourceItemDto> CreateResourceAsync(CreateResourceRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Resources", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje resursa nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ResourceItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje resursa nije ispravan.");

            return result;
        }

        public async Task<ResourceItemDto> UpdateResourceAsync(long resourceId, UpdateResourceRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/Resources/{resourceId}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena resursa nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<ResourceItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu resursa nije ispravan.");

            return result;
        }

        public async Task ActivateResourceAsync(long resourceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Resources/{resourceId}/activate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Aktivacija resursa nije uspela. Server vraća: {responseText}");
        }

        public async Task DeactivateResourceAsync(long resourceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.PostAsync($"api/Resources/{resourceId}/deactivate", null, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Deaktivacija resursa nije uspela. Server vraća: {responseText}");
        }

        public async Task DeleteResourceAsync(long resourceId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"api/Resources/{resourceId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brisanje resursa nije uspelo. Server vraća: {responseText}");
        }

        public async Task<List<BusinessWorkingHourDto>> GetBusinessWorkingHoursAsync(long businessId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/WorkingHours/business?businessId={businessId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje radnog vremena nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/business?businessId={businessId}. " +
                    $"Server vraća: {responseText}");

            var result = new List<BusinessWorkingHourDto>();

            using var document = JsonDocument.Parse(responseText);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var dayOfWeek = item.TryGetProperty("dayOfWeek", out var dayProp)
                    ? dayProp.GetInt32()
                    : 1;

                var startTime = item.TryGetProperty("startTime", out var startProp)
                    ? startProp.GetString()
                    : "09:00";

                var endTime = item.TryGetProperty("endTime", out var endProp)
                    ? endProp.GetString()
                    : "17:00";

                var isClosed = item.TryGetProperty("isClosed", out var closedProp) &&
                               closedProp.ValueKind == JsonValueKind.True;

                result.Add(new BusinessWorkingHourDto
                {
                    DayOfWeek = dayOfWeek,
                    StartTime = string.IsNullOrWhiteSpace(startTime) ? "09:00" : startTime,
                    EndTime = string.IsNullOrWhiteSpace(endTime) ? "17:00" : endTime,
                    IsWorkingDay = !isClosed
                });
            }

            return result;
        }

        public async Task<List<TimeOffBlockDto>> GetTimeOffBlocksAsync(
    long businessId,
    long? staffMemberId,
    DateTime? fromUtc,
    DateTime? toUtc,
    CancellationToken cancellationToken = default)
        {
            var query = $"api/WorkingHours/blocks?businessId={businessId}";

            if (staffMemberId.HasValue)
                query += $"&staffMemberId={staffMemberId.Value}";

            if (fromUtc.HasValue)
                query += $"&fromUtc={Uri.EscapeDataString(fromUtc.Value.ToString("o"))}";

            if (toUtc.HasValue)
                query += $"&toUtc={Uri.EscapeDataString(toUtc.Value.ToString("o"))}";

            using var response = await _httpClient.GetAsync(query, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje odsustava nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta={query}. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<TimeOffBlockDto>>(responseText, _jsonOptions);
            return result ?? new List<TimeOffBlockDto>();
        }

        public async Task<TimeOffBlockDto> CreateTimeOffBlockAsync(
            CreateTimeOffBlockRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/WorkingHours/blocks", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Kreiranje odsustva nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/blocks. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<TimeOffBlockDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje odsustva nije ispravan.");

            return result;
        }

        public async Task DeleteTimeOffBlockAsync(long id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"api/WorkingHours/blocks/{id}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Brisanje odsustva nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/blocks/{id}. " +
                    $"Server vraća: {responseText}");
        }

        public async Task<List<AvailableSlotDto>> GetAvailableSlotsAsync(
            long businessId,
            long serviceId,
            long? staffMemberId,
            long? resourceId,
            DateTime date,
            CancellationToken cancellationToken = default)
        {
            var requestDate = date.Date;

            var body = new
            {
                businessId,
                serviceId,
                staffMemberId,
                resourceId,
                date = requestDate
            };

            var json = JsonSerializer.Serialize(body);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Scheduling/available-slots", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje slobodnih termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<AvailableSlotDto>>(responseText, _jsonOptions);
            return result ?? new List<AvailableSlotDto>();
        }

        public async Task<List<BusinessWorkingHourDto>> GetStaffWorkingHoursAsync(long staffId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/WorkingHours/staff?staffMemberId={staffId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje radnog vremena radnika nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/staff?staffMemberId={staffId}. " +
                    $"Server vraća: {responseText}");

            var result = new List<BusinessWorkingHourDto>();

            using var document = JsonDocument.Parse(responseText);

            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var item in document.RootElement.EnumerateArray())
            {
                var uiDayOfWeek = item.TryGetProperty("dayOfWeek", out var dayProp)
                    ? dayProp.GetInt32()
                    : 1;

                var startTime = item.TryGetProperty("startTime", out var startProp)
                    ? startProp.GetString()
                    : "09:00";

                var endTime = item.TryGetProperty("endTime", out var endProp)
                    ? endProp.GetString()
                    : "17:00";

                var isClosed = item.TryGetProperty("isClosed", out var closedProp) &&
                               closedProp.ValueKind == JsonValueKind.True;

                result.Add(new BusinessWorkingHourDto
                {
                    DayOfWeek = uiDayOfWeek,
                    StartTime = string.IsNullOrWhiteSpace(startTime) ? "09:00" : startTime,
                    EndTime = string.IsNullOrWhiteSpace(endTime) ? "17:00" : endTime,
                    IsWorkingDay = !isClosed
                });
            }

            return result;
        }

        public async Task<List<StaffScheduleOverrideDto>> GetStaffScheduleOverridesAsync(
    long staffMemberId,
    DateTime? from,
    DateTime? to,
    CancellationToken cancellationToken = default)
        {
            var query = $"api/StaffSchedules/overrides?staffMemberId={staffMemberId}";

            if (from.HasValue)
                query += $"&from={Uri.EscapeDataString(from.Value.ToString("o"))}";

            if (to.HasValue)
                query += $"&to={Uri.EscapeDataString(to.Value.ToString("o"))}";

            using var response = await _httpClient.GetAsync(query, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Učitavanje izuzetaka smene nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). Ruta={query}. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<StaffScheduleOverrideDto>>(responseText, _jsonOptions);
            return result ?? new List<StaffScheduleOverrideDto>();
        }

        public async Task<StaffScheduleOverrideDto> CreateStaffScheduleOverrideAsync(
            CreateOrUpdateStaffScheduleOverrideRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/StaffSchedules/overrides", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Kreiranje izuzetka smene nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/StaffSchedules/overrides. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<StaffScheduleOverrideDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Server nije vratio ispravan odgovor za izuzetak smene.");

            return result;
        }

        public async Task<StaffScheduleOverrideDto> UpdateStaffScheduleOverrideAsync(
            long id,
            CreateOrUpdateStaffScheduleOverrideRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/StaffSchedules/overrides/{id}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Izmena izuzetka smene nije uspela. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/StaffSchedules/overrides/{id}. " +
                    $"Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<StaffScheduleOverrideDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Server nije vratio ispravan odgovor za izmenu izuzetka smene.");

            return result;
        }

        public async Task DeleteStaffScheduleOverrideAsync(long id, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.DeleteAsync($"api/StaffSchedules/overrides/{id}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Brisanje izuzetka smene nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/StaffSchedules/overrides/{id}. " +
                    $"Server vraća: {responseText}");
        }

        public async Task UpdateStaffWorkingHoursAsync(
            object request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/WorkingHours/staff", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Čuvanje radnog vremena radnika nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/staff. " +
                    $"Server vraća: {responseText}");
        }

        public async Task UpdateBusinessWorkingHoursAsync(object request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/WorkingHours/business", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException(
                    $"Čuvanje radnog vremena nije uspelo. " +
                    $"Status={(int)response.StatusCode} ({response.StatusCode}). " +
                    $"Ruta=api/WorkingHours/business. " +
                    $"Server vraća: {responseText}");
        }

        public async Task<BusinessDetailsDto> GetBusinessByIdAsync(long businessId, CancellationToken cancellationToken = default)
        {
            using var response = await _httpClient.GetAsync($"api/Businesses/{businessId}", cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje podataka o biznisu nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<BusinessDetailsDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za biznis nije ispravan.");

            return result;
        }

        public async Task<BusinessDetailsDto> UpdateBusinessAsync(long businessId, UpdateBusinessRequestDto request, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/Businesses/{businessId}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Čuvanje podataka o biznisu nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<BusinessDetailsDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za čuvanje biznisa nije ispravan.");

            return result;
        }

        public async Task<AppointmentListItemDto> CreateOwnerAppointmentAsync(
            CreateOwnerAppointmentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var startAtUtc = request.StartAtUtc.Kind switch
            {
                DateTimeKind.Utc => request.StartAtUtc,
                DateTimeKind.Local => request.StartAtUtc.ToUniversalTime(),
                _ => DateTime.SpecifyKind(request.StartAtUtc, DateTimeKind.Utc)
            };

            var body = new
            {
                businessId = request.BusinessId,
                serviceId = request.ServiceId,
                primaryStaffMemberId = request.PrimaryStaffMemberId,
                resourceId = request.ResourceId,
                businessCustomerId = request.BusinessCustomerId,
                startAtUtc = startAtUtc.ToString("O"),
                customerName = request.CustomerName,
                customerPhone = request.CustomerPhone,
                notes = request.Notes,
                ignoreAvailabilityRules = request.IgnoreAvailabilityRules,
                ignoreWorkingHours = request.IgnoreWorkingHours,
                ignoreTimeOffBlocks = request.IgnoreTimeOffBlocks,
                ignoreAppointmentConflicts = request.IgnoreAppointmentConflicts,
                finalDurationMin = request.FinalDurationMin
            };

            var json = JsonSerializer.Serialize(body);

            System.Diagnostics.Debug.WriteLine("========== CREATE OWNER APPOINTMENT JSON ==========");
            System.Diagnostics.Debug.WriteLine(json);
            System.Diagnostics.Debug.WriteLine("===================================================");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/owner-create", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Kreiranje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentListItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za kreiranje termina nije ispravan.");

            return result;
        }

        public async Task<List<StaffServiceAssignmentDto>> GetStaffServicesAsync(long staffId)
        {
            using var response = await _httpClient.GetAsync($"api/Staff/{staffId}/services");

            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Učitavanje usluga radnika nije uspelo. Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/Staff/{staffId}/services. Server vraća: {raw}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<StaffServiceAssignmentDto>>();
            return result ?? new List<StaffServiceAssignmentDto>();
        }

        public async Task UpdateStaffServicesAsync(long staffId, UpdateStaffServicesRequestDto request)
        {
            using var response = await _httpClient.PutAsJsonAsync($"api/Staff/{staffId}/services", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Čuvanje usluga radnika nije uspelo. Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/Staff/{staffId}/services. Server vraća: {error}");
            }
        }

        public async Task<List<StaffResourceAssignmentDto>> GetStaffResourcesAsync(long staffId)
        {
            using var response = await _httpClient.GetAsync($"api/Staff/{staffId}/resources");

            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Učitavanje resursa radnika nije uspelo. Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/Staff/{staffId}/resources. Server vraća: {raw}");
            }

            var result = await response.Content.ReadFromJsonAsync<List<StaffResourceAssignmentDto>>();
            return result ?? new List<StaffResourceAssignmentDto>();
        }

        public async Task UpdateStaffResourcesAsync(long staffId, UpdateStaffResourcesRequestDto request)
        {
            using var response = await _httpClient.PutAsJsonAsync($"api/Staff/{staffId}/resources", request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException(
                    $"Čuvanje resursa radnika nije uspelo. Status={(int)response.StatusCode} ({response.StatusCode}). Ruta=api/Staff/{staffId}/resources. Server vraća: {error}");
            }
        }

        public async Task<AppointmentActionResponseDto> ApproveAppointmentAsync(
    ApproveAppointmentRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/approve", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Prihvatanje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za prihvatanje termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentActionResponseDto> RejectAppointmentAsync(
            RejectAppointmentRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/reject", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Odbijanje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za odbijanje termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentChangeActionResponseDto> AcceptRescheduleRequestAsync(
            AcceptRescheduleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/accept-reschedule-request", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Prihvatanje promene termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentChangeActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za prihvatanje promene termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentChangeActionResponseDto> RejectRescheduleRequestAsync(
            RejectRescheduleRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/reject-reschedule-request", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Odbijanje promene termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentChangeActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za odbijanje promene termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentActionResponseDto> CompleteAppointmentAsync(
            UpdateConfirmedAppointmentStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/complete", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Zatvaranje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za završetak termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentActionResponseDto> MarkNoShowAsync(
            UpdateConfirmedAppointmentStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/no-show", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Evidentiranje nedolaska nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za nedolazak nije ispravan.");

            return result;
        }

        public async Task<AppointmentActionResponseDto> CancelAppointmentAsync(
            UpdateConfirmedAppointmentStatusRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/cancel", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Otkazivanje termina nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za otkazivanje termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentChangeActionResponseDto> ProposeAppointmentTimeAsync(
    ProposeAppointmentTimeRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/propose-time", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Predlog novog termina nije uspeo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentChangeActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za predlog novog termina nije ispravan.");

            return result;
        }

        public async Task<AppointmentChangeActionResponseDto> ProposeDelayAsync(
    ProposeDelayRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/Appointments/propose-delay", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Predlog odlaganja nije uspeo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<AppointmentChangeActionResponseDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za predlog odlaganja nije ispravan.");

            return result;
        }

        public async Task<List<BusinessCustomerItemDto>> SearchBusinessCustomersAsync(
    long businessId,
    string query,
    int limit = 10,
    CancellationToken cancellationToken = default)
        {
            if (businessId <= 0)
                return new List<BusinessCustomerItemDto>();

            if (string.IsNullOrWhiteSpace(query))
                return new List<BusinessCustomerItemDto>();

            query = query.Trim();

            if (query.Length < 2)
                return new List<BusinessCustomerItemDto>();

            limit = Math.Clamp(limit, 1, 20);

            var url =
                $"api/BusinessCustomers/search?businessId={businessId}&q={Uri.EscapeDataString(query)}&limit={limit}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Pretraga klijenata nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<BusinessCustomerItemDto>>(responseText, _jsonOptions);
            return result ?? new List<BusinessCustomerItemDto>();
        }

        public async Task<BusinessCustomerItemDto> CreateBusinessCustomerAsync(
    CreateBusinessCustomerRequestDto request,
    CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync("api/BusinessCustomers", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Čuvanje klijenta nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<BusinessCustomerItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za čuvanje klijenta nije ispravan.");

            return result;
        }

        public async Task<List<BusinessCustomerItemDto>> GetBusinessCustomersAsync(
    long businessId,
    CancellationToken cancellationToken = default)
        {
            if (businessId <= 0)
                return new List<BusinessCustomerItemDto>();

            var url = $"api/BusinessCustomers?businessId={businessId}";

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Učitavanje klijenata nije uspelo. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<List<BusinessCustomerItemDto>>(responseText, _jsonOptions);
            return result ?? new List<BusinessCustomerItemDto>();
        }

        public async Task<BusinessCustomerItemDto> UpdateBusinessCustomerAsync(
            long id,
            UpdateBusinessCustomerRequestDto request,
            CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(request);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PutAsync($"api/BusinessCustomers/{id}", content, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Izmena klijenta nije uspela. Server vraća: {responseText}");

            var result = JsonSerializer.Deserialize<BusinessCustomerItemDto>(responseText, _jsonOptions);
            if (result is null)
                throw new InvalidOperationException("Odgovor servera za izmenu klijenta nije ispravan.");

            return result;
        }
    }

}