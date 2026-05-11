using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Contracts.Resources;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class ServiceResourceRequirementsWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ServiceResourceRequirementsWorkflowTests(CustomWebApplicationFactory factory)
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

    private async Task<long> AddExtraResourceAsync(long businessId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        return await AppointmentIntegrationTestData.AddResourceAsync(db, businessId, name);
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

    private async Task<ServiceResourceRequirementDto> CreateRequirementAsync(
        HttpClient client,
        long serviceId,
        long resourceId,
        bool isRequired = true)
    {
        var response = await client.PostAsJsonAsync("/api/ServiceResourceRequirements", new CreateServiceResourceRequirementRequest
        {
            ServiceId = serviceId,
            ResourceId = resourceId,
            IsRequired = isRequired
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResourceRequirementDto>();
        Assert.NotNull(body);

        return body!;
    }

    [Fact]
    public async Task GetAll_WithoutToken_ShouldReturnUnauthorized()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/ServiceResourceRequirements?serviceId={ids.ServiceId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 1");
        await CreateRequirementAsync(ownerClient, ids.ServiceId, extraResourceId, isRequired: true);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/ServiceResourceRequirements?serviceId={ids.ServiceId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceResourceRequirementDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.ServiceId == ids.ServiceId && x.ResourceId == extraResourceId);
    }

    [Fact]
    public async Task Create_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/ServiceResourceRequirements", new CreateServiceResourceRequirementRequest
        {
            ServiceId = ids.ServiceId,
            ResourceId = ids.ResourceId,
            IsRequired = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 2");

        var response = await managerClient.PostAsJsonAsync("/api/ServiceResourceRequirements", new CreateServiceResourceRequirementRequest
        {
            ServiceId = ids.ServiceId,
            ResourceId = extraResourceId,
            IsRequired = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResourceRequirementDto>();
        Assert.NotNull(body);
        Assert.Equal(ids.ServiceId, body!.ServiceId);
        Assert.Equal(extraResourceId, body.ResourceId);
        Assert.True(body.IsRequired);
    }

    [Fact]
    public async Task Update_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 3");
        var created = await CreateRequirementAsync(ownerClient, ids.ServiceId, extraResourceId, isRequired: true);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PutAsJsonAsync($"/api/ServiceResourceRequirements/{created.Id}", new UpdateServiceResourceRequirementRequest
        {
            IsRequired = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 4");
        var created = await CreateRequirementAsync(ownerClient, ids.ServiceId, extraResourceId, isRequired: true);

        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PutAsJsonAsync($"/api/ServiceResourceRequirements/{created.Id}", new UpdateServiceResourceRequirementRequest
        {
            IsRequired = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceResourceRequirementDto>();
        Assert.NotNull(body);
        Assert.False(body!.IsRequired);
    }

    [Fact]
    public async Task Delete_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 5");
        var created = await CreateRequirementAsync(ownerClient, ids.ServiceId, extraResourceId, isRequired: true);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.DeleteAsync($"/api/ServiceResourceRequirements/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WithManagerMembership_ShouldReturnNoContent()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var extraResourceId = await AddExtraResourceAsync(ids.BusinessId, "Extra resource 6");
        var created = await CreateRequirementAsync(ownerClient, ids.ServiceId, extraResourceId, isRequired: true);

        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.DeleteAsync($"/api/ServiceResourceRequirements/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}