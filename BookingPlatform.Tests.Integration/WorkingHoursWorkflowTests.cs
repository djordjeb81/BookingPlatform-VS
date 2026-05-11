using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class WorkingHoursWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkingHoursWorkflowTests(CustomWebApplicationFactory factory)
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
    public async Task GetBusinessHours_WithoutToken_ShouldReturnUnauthorized()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/WorkingHours/business?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBusinessHours_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/WorkingHours/business?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<BusinessWorkingHourDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task SetBusinessHour_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/WorkingHours/business", new SetBusinessWorkingHourRequest
        {
            BusinessId = ids.BusinessId,
            DayOfWeek = 1,
            StartTime = "08:00",
            EndTime = "16:00",
            IsClosed = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetBusinessHour_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/WorkingHours/business", new SetBusinessWorkingHourRequest
        {
            BusinessId = ids.BusinessId,
            DayOfWeek = 1,
            StartTime = "08:00",
            EndTime = "16:00",
            IsClosed = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<BusinessWorkingHourDto>();
        Assert.NotNull(body);
        Assert.Equal(ids.BusinessId, body!.BusinessId);
        Assert.Equal(1, body.DayOfWeek);
        Assert.Equal("08:00", body.StartTime);
        Assert.Equal("16:00", body.EndTime);
        Assert.False(body.IsClosed);
    }

    [Fact]
    public async Task GetStaffHours_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/WorkingHours/staff?staffMemberId={ids.StaffMemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<StaffWorkingHourDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task SetStaffHour_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/WorkingHours/staff", new SetStaffWorkingHourRequest
        {
            StaffMemberId = ids.StaffMemberId,
            DayOfWeek = 2,
            StartTime = "10:00",
            EndTime = "15:00",
            IsClosed = false
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetStaffHour_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/WorkingHours/staff", new SetStaffWorkingHourRequest
        {
            StaffMemberId = ids.StaffMemberId,
            DayOfWeek = 2,
            StartTime = "10:00",
            EndTime = "15:00",
            IsClosed = false
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<StaffWorkingHourDto>();
        Assert.NotNull(body);
        Assert.Equal(ids.StaffMemberId, body!.StaffMemberId);
        Assert.Equal(2, body.DayOfWeek);
        Assert.Equal("10:00", body.StartTime);
        Assert.Equal("15:00", body.EndTime);
        Assert.False(body.IsClosed);
    }

    [Fact]
    public async Task GetBlocks_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createBlockResponse = await ownerClient.PostAsJsonAsync("/api/WorkingHours/blocks", new CreateTimeOffBlockRequest
        {
            BusinessId = ids.BusinessId,
            StaffMemberId = ids.StaffMemberId,
            StartAtUtc = new DateTime(2026, 4, 14, 12, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 4, 14, 13, 0, 0, DateTimeKind.Utc),
            BlockType = 1,
            Reason = "Pauza"
        });

        Assert.Equal(HttpStatusCode.OK, createBlockResponse.StatusCode);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/WorkingHours/blocks?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<TimeOffBlockDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.Reason == "Pauza");
    }

    [Fact]
    public async Task CreateBlock_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.PostAsJsonAsync("/api/WorkingHours/blocks", new CreateTimeOffBlockRequest
        {
            BusinessId = ids.BusinessId,
            StaffMemberId = ids.StaffMemberId,
            StartAtUtc = new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 4, 15, 13, 0, 0, DateTimeKind.Utc),
            BlockType = 1,
            Reason = "Staff ne sme create"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateBlock_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var response = await managerClient.PostAsJsonAsync("/api/WorkingHours/blocks", new CreateTimeOffBlockRequest
        {
            BusinessId = ids.BusinessId,
            StaffMemberId = ids.StaffMemberId,
            StartAtUtc = new DateTime(2026, 4, 16, 12, 0, 0, DateTimeKind.Utc),
            EndAtUtc = new DateTime(2026, 4, 16, 13, 0, 0, DateTimeKind.Utc),
            BlockType = 1,
            Reason = "Manager block"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TimeOffBlockDto>();
        Assert.NotNull(body);
        Assert.Equal(ids.BusinessId, body!.BusinessId);
        Assert.Equal(ids.StaffMemberId, body.StaffMemberId);
        Assert.Equal("Manager block", body.Reason);
    }
}