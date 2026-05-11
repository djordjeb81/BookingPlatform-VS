using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Contracts.Services;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class ServiceStepsWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceStepsWorkflowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<AppointmentSeedIds> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        await AppointmentIntegrationTestData.ResetAsync(db);
        return await AppointmentIntegrationTestData.SeedBasicSchedulingScenarioAsync(db);
    }

    private async Task<HttpClient> RegisterClientAsync(
        string email,
        string fullName,
        long? initialBusinessId = null)
    {
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "test123",
            FullName = fullName,
            InitialBusinessId = initialBusinessId
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }

    private async Task<HttpClient> CreateOwnerClientAsync(long businessId)
    {
        return await RegisterClientAsync(
            $"owner-{Guid.NewGuid():N}@test.rs",
            "Test Owner",
            businessId);
    }

    private async Task<HttpClient> CreateBusinessMemberClientAsync(
        HttpClient ownerClient,
        long businessId,
        string role)
    {
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.rs";
        var client = await RegisterClientAsync(email, $"Test {role}");

        var upsertResponse = await ownerClient.PostAsJsonAsync("/api/BusinessUsers/upsert", new UpsertBusinessMembershipRequest
        {
            BusinessId = businessId,
            UserEmail = email,
            Role = role
        });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        return client;
    }

    private async Task<ServiceStepDto> CreateStepAsync(
        HttpClient client,
        long serviceId,
        int stepOrder,
        string name,
        int durationMin)
    {
        var response = await client.PostAsJsonAsync("/api/ServiceSteps", new CreateServiceStepRequest
        {
            ServiceId = serviceId,
            StepOrder = stepOrder,
            Name = name,
            DurationMin = durationMin,
            ClientPresenceRequired = true,
            SameStaffAsPrevious = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceStepDto>();
        Assert.NotNull(body);

        return body!;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ShouldReturnUnauthorized()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/ServiceSteps?serviceId={ids.ServiceId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        await CreateStepAsync(ownerClient, ids.ServiceId, 1, "Priprema", 10);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/ServiceSteps?serviceId={ids.ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceStepDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.ServiceId == ids.ServiceId && x.Name == "Priprema");
    }

    [Fact]
    public async Task Create_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/ServiceSteps", new CreateServiceStepRequest
        {
            ServiceId = ids.ServiceId,
            StepOrder = 1,
            Name = "Staff step",
            DurationMin = 10,
            ClientPresenceRequired = true,
            SameStaffAsPrevious = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/ServiceSteps", new CreateServiceStepRequest
        {
            ServiceId = ids.ServiceId,
            StepOrder = 1,
            Name = "Manager step",
            DurationMin = 15,
            ClientPresenceRequired = true,
            SameStaffAsPrevious = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceStepDto>();
        Assert.NotNull(body);
        Assert.Equal(ids.ServiceId, body!.ServiceId);
        Assert.Equal(1, body.StepOrder);
        Assert.Equal("Manager step", body.Name);
        Assert.Equal(15, body.DurationMin);
    }

    [Fact]
    public async Task Update_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var created = await CreateStepAsync(ownerClient, ids.ServiceId, 1, "Korak 1", 10);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PutAsJsonAsync($"/api/ServiceSteps/{created.Id}", new UpdateServiceStepRequest
        {
            StepOrder = 1,
            Name = "Izmenjen korak",
            DurationMin = 20,
            ClientPresenceRequired = true,
            SameStaffAsPrevious = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var created = await CreateStepAsync(ownerClient, ids.ServiceId, 1, "Korak 1", 10);

        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PutAsJsonAsync($"/api/ServiceSteps/{created.Id}", new UpdateServiceStepRequest
        {
            StepOrder = 2,
            Name = "Izmenjen korak",
            DurationMin = 20,
            ClientPresenceRequired = false,
            SameStaffAsPrevious = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceStepDto>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.StepOrder);
        Assert.Equal("Izmenjen korak", body.Name);
        Assert.Equal(20, body.DurationMin);
        Assert.False(body.ClientPresenceRequired);
        Assert.True(body.SameStaffAsPrevious);
    }

    [Fact]
    public async Task Delete_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var created = await CreateStepAsync(ownerClient, ids.ServiceId, 1, "Korak 1", 10);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.DeleteAsync($"/api/ServiceSteps/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithManagerMembership_ShouldReturnNoContent()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var created = await CreateStepAsync(ownerClient, ids.ServiceId, 1, "Korak 1", 10);

        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.DeleteAsync($"/api/ServiceSteps/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}