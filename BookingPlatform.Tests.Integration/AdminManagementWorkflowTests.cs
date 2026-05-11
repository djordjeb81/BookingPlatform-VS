using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Contracts.Businesses;
using BookingPlatform.Contracts.Resources;
using BookingPlatform.Contracts.Services;
using BookingPlatform.Contracts.Staff;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class AdminManagementWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminManagementWorkflowTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Businesses_GetAll_WithoutToken_ShouldReturnUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/Businesses");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Businesses_Create_ShouldAutomaticallyAssignOwnerMembership()
    {
        var client = await RegisterClientAsync(
            $"new-owner-{Guid.NewGuid():N}@test.rs",
            "New Owner");

        var createResponse = await client.PostAsJsonAsync("/api/Businesses", new CreateBusinessRequest
        {
            Name = "Nova Radnja",
            BusinessType = 1,
            Description = "Test",
            Phone = "0600000001",
            Email = "nova@test.rs",
            SlotIntervalMin = 30
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<BusinessDto>();
        Assert.NotNull(created);

        var businessesResponse = await client.GetAsync("/api/Businesses");
        Assert.Equal(HttpStatusCode.OK, businessesResponse.StatusCode);

        var businesses = await businessesResponse.Content.ReadFromJsonAsync<List<BusinessDto>>();
        Assert.NotNull(businesses);

        Assert.Contains(businesses!, x => x.Id == created!.Id && x.Name == "Nova Radnja");
    }

    [Fact]
    public async Task Services_GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/Services?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ServiceDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task Services_Create_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/Services", new CreateServiceRequest
        {
            BusinessId = ids.BusinessId,
            Name = "Nova usluga",
            Description = "Opis",
            BasePrice = 1500,
            EstimatedDurationMin = 30,
            BookingStrategyType = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Services_Create_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/Services", new CreateServiceRequest
        {
            BusinessId = ids.BusinessId,
            Name = "Manager usluga",
            Description = "Opis",
            BasePrice = 2000,
            EstimatedDurationMin = 45,
            BookingStrategyType = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ServiceDto>();
        Assert.NotNull(body);
        Assert.Equal("Manager usluga", body!.Name);
    }

    [Fact]
    public async Task Resources_GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/Resources?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ResourceDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task Resources_Create_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/Resources", new CreateResourceRequest
        {
            BusinessId = ids.BusinessId,
            Name = "Nova stolica",
            ResourceType = 1,
            Capacity = 1
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Resources_Create_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/Resources", new CreateResourceRequest
        {
            BusinessId = ids.BusinessId,
            Name = "Manager resurs",
            ResourceType = 1,
            Capacity = 1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ResourceDto>();
        Assert.NotNull(body);
        Assert.Equal("Manager resurs", body!.Name);
    }

    [Fact]
    public async Task Staff_GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/Staff?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<StaffMemberDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task Staff_Create_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/Staff", new CreateStaffMemberRequest
        {
            BusinessId = ids.BusinessId,
            DisplayName = "Novi zaposleni",
            Title = "Frizer",
            IsBookable = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Staff_Create_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/Staff", new CreateStaffMemberRequest
        {
            BusinessId = ids.BusinessId,
            DisplayName = "Manager zaposleni",
            Title = "Asistent",
            IsBookable = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<StaffMemberDto>();
        Assert.NotNull(body);
        Assert.Equal("Manager zaposleni", body!.DisplayName);
    }

    [Fact]
    public async Task Businesses_Update_WithManagerMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PutAsJsonAsync($"/api/Businesses/{ids.BusinessId}", new UpdateBusinessRequest
        {
            Name = "Izmena od managera",
            BusinessType = 1,
            Description = "Opis",
            Phone = "0609999999",
            Email = "manager@test.rs",
            SlotIntervalMin = 30,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Businesses_Update_WithOwnerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var response = await ownerClient.PutAsJsonAsync($"/api/Businesses/{ids.BusinessId}", new UpdateBusinessRequest
        {
            Name = "Izmenjena radnja",
            BusinessType = 1,
            Description = "Opis",
            Phone = "0607777777",
            Email = "owner@test.rs",
            SlotIntervalMin = 30,
            IsActive = true
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BusinessDto>();
        Assert.NotNull(body);
        Assert.Equal("Izmenjena radnja", body!.Name);
    }
}