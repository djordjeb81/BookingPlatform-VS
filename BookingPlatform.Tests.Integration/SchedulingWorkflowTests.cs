using System.Net;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Contracts.Scheduling;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using BookingPlatform.Domain.Staff;
using System.Net.Http.Headers;
using BookingPlatform.Contracts.Auth;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class SchedulingWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SchedulingWorkflowTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task AvailableSlots_ShouldReturn_ThirtyMinuteGrid()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var request = new SearchAvailableSlotsRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            Date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/available-slots", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AvailableSlotDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);

        Assert.Equal(new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc), body[0].StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc), body[0].EndAtUtc);

        Assert.Contains(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
        Assert.Contains(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 16, 30, 0, DateTimeKind.Utc));
    }

    private async Task<HttpClient> CreateOwnerClientAsync(long businessId)
    {
        var client = _factory.CreateClient();

        var email = $"owner-{Guid.NewGuid():N}@test.rs";

        var registerResponse = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "test123",
            FullName = "Test Owner",
            InitialBusinessId = businessId
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }

    private async Task<HttpClient> CreateBusinessMemberClientAsync(
        HttpClient ownerClient,
        long businessId,
        string role)
    {
        var client = _factory.CreateClient();

        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@test.rs";

        var registerResponse = await client.PostAsJsonAsync("/api/Auth/register", new RegisterRequest
        {
            Email = email,
            Password = "test123",
            FullName = $"Test {role}"
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);

        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var upsertResponse = await ownerClient.PostAsJsonAsync("/api/BusinessUsers/upsert", new UpsertBusinessMembershipRequest
        {
            BusinessId = businessId,
            UserEmail = email,
            Role = role
        });

        Assert.Equal(HttpStatusCode.OK, upsertResponse.StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.Token);

        return client;
    }

    [Fact]
    public async Task AvailableSlots_AfterExtendedConfirmedAppointment_ShouldSkipCoveredSlots()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Petar",
            CustomerPhone = "0601111111",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            Notes = "Produženi termin"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var approveRequest = new ApproveAppointmentRequest
        {
            AppointmentId = created!.Id,
            FinalDurationMin = 90
        };

        var approveResponse = await client.PostAsJsonAsync("/api/Appointments/approve", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var slotsRequest = new SearchAvailableSlotsRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            Date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var slotsResponse = await client.PostAsJsonAsync("/api/Scheduling/available-slots", slotsRequest);
        Assert.Equal(HttpStatusCode.OK, slotsResponse.StatusCode);

        var body = await slotsResponse.Content.ReadFromJsonAsync<List<AvailableSlotDto>>();
        Assert.NotNull(body);

        Assert.DoesNotContain(body!, x => x.StartAtUtc == new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
        Assert.DoesNotContain(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 10, 30, 0, DateTimeKind.Utc));
        Assert.DoesNotContain(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc));

        Assert.Contains(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 11, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task FirstAvailable_ShouldReturnEarliestFreeSlot()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var request = new FirstAvailableSearchRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            StartDate = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
            SearchDays = 3
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/first-available", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<FirstAvailableResultDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);

        var first = body!.First();
        Assert.Equal(ids.StaffMemberId, first.StaffMemberId);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc), first.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc), first.EndAtUtc);
    }

    [Fact]
    public async Task DailyCalendar_ShouldReturnAppointmentsAndBlocks()
    {
        var ids = await SeedAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

            db.Appointments.Add(new Appointment
            {
                BusinessId = ids.BusinessId,
                ServiceId = ids.ServiceId,
                PrimaryStaffMemberId = ids.StaffMemberId,
                CustomerName = "Kalendar Klijent",
                CustomerPhone = "0602222222",
                Status = AppointmentStatus.Confirmed,
                StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
                EndAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc),
                Notes = "Kalendar termin",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            db.TimeOffBlocks.Add(new TimeOffBlock
            {
                BusinessId = ids.BusinessId,
                StaffMemberId = ids.StaffMemberId,
                StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
                EndAtUtc = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc),
                BlockType = (TimeOffBlockType)1,
                Reason = "Pauza",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var url =
            $"/api/Scheduling/daily-calendar?businessId={ids.BusinessId}" +
            $"&date=2026-04-13T00:00:00Z" +
            $"&staffMemberId={ids.StaffMemberId}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DailyCalendarItemDto>>();
        Assert.NotNull(body);

        Assert.Contains(body!, x =>
            x.ItemType == "Appointment" &&
            x.CustomerName == "Kalendar Klijent" &&
            x.StartAtUtc == new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc));

        Assert.Contains(body, x =>
            x.ItemType == "Block" &&
            x.Title == "Pauza" &&
            x.StartAtUtc == new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc));
    }

    private async Task<long> AddSecondStaffMemberAsync(long businessId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        var now = DateTime.UtcNow;

        var staff = new StaffMember
        {
            BusinessId = businessId,
            DisplayName = "Mina",
            Title = "Frizer",
            IsBookable = true,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        db.StaffMembers.Add(staff);
        await db.SaveChangesAsync();

        for (var day = 1; day <= 7; day++)
        {
            var isSunday = day == 7;

            db.StaffWorkingHours.Add(new StaffWorkingHour
            {
                StaffMemberId = staff.Id,
                DayOfWeek = day,
                StartTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(9),
                EndTime = isSunday ? TimeSpan.Zero : TimeSpan.FromHours(17),
                IsClosed = isSunday
            });
        }

        await db.SaveChangesAsync();
        return staff.Id;
    }

    private async Task<long> AddExtraResourceAsync(long businessId, string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        return await AppointmentIntegrationTestData.AddResourceAsync(db, businessId, name);
    }

    private async Task ConfigureServiceResourceRequirementAsync(
        long serviceId,
        long resourceId,
        bool isRequired)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingDbContext>();

        await AppointmentIntegrationTestData.EnsureServiceResourceRequirementAsync(
            db,
            serviceId,
            resourceId,
            isRequired);
    }

    [Fact]
    public async Task AvailableSlots_WithBusyResourceOnDifferentStaff_ShouldSkipCoveredSlots()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "Resource Block",
            CustomerPhone = "0607000001",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var request = new SearchAvailableSlotsRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            Date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/available-slots", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AvailableSlotDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);

        Assert.DoesNotContain(body!, x => x.StartAtUtc == new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
        Assert.Contains(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc));
        Assert.Contains(body, x => x.StartAtUtc == new DateTime(2026, 4, 13, 10, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task FirstAvailable_WithBusyResourceOnDifferentStaff_ShouldReturnLaterSlot()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "First Available Block",
            CustomerPhone = "0607000002",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var request = new FirstAvailableSearchRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            StartDate = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
            SearchDays = 1
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/first-available", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<FirstAvailableResultDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);

        var first = body!.First();
        Assert.Equal(ids.StaffMemberId, first.StaffMemberId);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc), first.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc), first.EndAtUtc);
    }

    [Fact]
    public async Task AvailableSlots_WithoutResource_WhenServiceRequiresResource_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        await ConfigureServiceResourceRequirementAsync(ids.ServiceId, ids.ResourceId, isRequired: true);

        var client = _factory.CreateClient();

        var request = new SearchAvailableSlotsRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            Date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/available-slots", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("potrebno izabrati resurs", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AvailableSlots_WithResourceNotAllowedForService_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var otherResourceId = await AddExtraResourceAsync(ids.BusinessId, "Stolica X");

        var client = _factory.CreateClient();

        var request = new SearchAvailableSlotsRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            ResourceId = otherResourceId,
            Date = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc)
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/available-slots", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nije dozvoljen za izabranu uslugu", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FirstAvailable_WithoutResource_WhenServiceRequiresResource_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        await ConfigureServiceResourceRequirementAsync(ids.ServiceId, ids.ResourceId, isRequired: true);

        var client = _factory.CreateClient();

        var request = new FirstAvailableSearchRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            StartDate = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
            SearchDays = 1
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/first-available", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("potrebno izabrati resurs", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FirstAvailable_WithResourceNotAllowedForService_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var otherResourceId = await AddExtraResourceAsync(ids.BusinessId, "Stolica Y");

        var client = _factory.CreateClient();

        var request = new FirstAvailableSearchRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            StaffMemberId = ids.StaffMemberId,
            ResourceId = otherResourceId,
            StartDate = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Utc),
            SearchDays = 1
        };

        var response = await client.PostAsJsonAsync("/api/Scheduling/first-available", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nije dozvoljen za izabranu uslugu", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DailyCalendar_ShouldReturn_ResourceName_ForAppointmentItems()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Calendar resource",
            CustomerPhone = "0601200003",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var url =
            $"/api/Scheduling/daily-calendar?businessId={ids.BusinessId}" +
            $"&date=2026-04-13T00:00:00Z" +
            $"&staffMemberId={ids.StaffMemberId}";

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DailyCalendarItemDto>>();
        Assert.NotNull(body);

        Assert.Contains(body!, x =>
            x.ItemType == "Appointment" &&
            x.CustomerName == "Calendar resource" &&
            x.ResourceId == ids.ResourceId &&
            x.ResourceName == "Stolica 1");
    }

    [Fact]
    public async Task DailyCalendar_WithoutToken_ShouldReturnUnauthorized()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var url =
            $"/api/Scheduling/daily-calendar?businessId={ids.BusinessId}" +
            $"&date=2026-04-13T00:00:00Z" +
            $"&staffMemberId={ids.StaffMemberId}";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DailyCalendar_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff calendar test",
            CustomerPhone = "0601600001",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var url =
            $"/api/Scheduling/daily-calendar?businessId={ids.BusinessId}" +
            $"&date=2026-04-13T00:00:00Z" +
            $"&staffMemberId={ids.StaffMemberId}";

        var response = await staffClient.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<DailyCalendarItemDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.CustomerName == "Staff calendar test");
    }
}