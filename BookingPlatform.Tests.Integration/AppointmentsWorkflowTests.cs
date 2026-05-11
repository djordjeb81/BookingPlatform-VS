using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Contracts.Auth;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class AppointmentsWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentsWorkflowTests(CustomWebApplicationFactory factory)
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
    public async Task CustomerCreate_OnValidGrid_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var request = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Petar Petrovic",
            CustomerPhone = "0601234567",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            Notes = "Test"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(body);
        Assert.Equal("PendingApproval", body!.Status);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc), body.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc), body.EndAtUtc);
    }

    [Fact]
    public async Task CustomerCreate_OutsideGrid_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var request = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Petar Petrovic",
            CustomerPhone = "0601234567",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 10, 0, DateTimeKind.Utc),
            Notes = "Van grid-a"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAppointmentErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("outside_slot_grid", body!.ReasonCode);
        Assert.Contains("outside_slot_grid", body.ReasonCodes);
        Assert.True(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.False(body.HasResourceConflict);
    }

    [Fact]
    public async Task OwnerCreate_OutsideGridWithoutOverride_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Marko",
            CustomerPhone = "0611111111",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 10, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("outside_slot_grid", body);
    }

    [Fact]
    public async Task OwnerCreate_OutsideGridWithOverride_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Marko",
            CustomerPhone = "0611111111",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 10, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = true,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("manual_override", body);
        Assert.Contains("slot_grid", body);
    }

    [Fact]
    public async Task ProposeTime_OutsideGrid_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Nikola",
            CustomerPhone = "0622222222",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            Notes = "Za propose"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var proposeRequest = new ProposeAppointmentTimeRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 9, 10, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Van grid-a"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/propose-time", proposeRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("outside_slot_grid", body!.ReasonCode);
        Assert.Contains("outside_slot_grid", body.ReasonCodes);
        Assert.True(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.False(body.HasResourceConflict);
    }

    [Fact]
    public async Task Approve_WithFinalDuration_ShouldExtendAppointment()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Jovan",
            CustomerPhone = "0633333333",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            Notes = "Za approve"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var approveRequest = new ApproveAppointmentRequest
        {
            AppointmentId = created!.Id,
            FinalDurationMin = 90
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/approve", approveRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentActionResponse>();
        Assert.NotNull(body);
        Assert.Equal(new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc), body!.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc), body.EndAtUtc);
    }

    [Fact]
    public async Task CustomerCreate_WithoutResource_WhenServiceRequiresResource_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        await ConfigureServiceResourceRequirementAsync(ids.ServiceId, ids.ResourceId, isRequired: true);

        var client = _factory.CreateClient();

        var request = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Required Resource Customer",
            CustomerPhone = "0608000001",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            Notes = "Bez resource"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAppointmentErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_required", body!.ReasonCode);
        Assert.Contains("resource_required", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.False(body.HasResourceConflict);
    }

    [Fact]
    public async Task CustomerCreate_WithResourceNotAllowedForService_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var otherResourceId = await AddExtraResourceAsync(ids.BusinessId, "Stolica 2");

        var client = _factory.CreateClient();

        var request = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = otherResourceId,
            CustomerName = "Wrong Resource Customer",
            CustomerPhone = "0608000002",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc),
            Notes = "Nedozvoljen resource"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAppointmentErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_not_allowed_for_service", body!.ReasonCode);
        Assert.Contains("resource_not_allowed_for_service", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.False(body.HasResourceConflict);
    }

    [Fact]
    public async Task OwnerCreate_WithoutResource_WhenServiceRequiresResource_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        await ConfigureServiceResourceRequirementAsync(ids.ServiceId, ids.ResourceId, isRequired: true);

        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Owner Required Resource",
            CustomerPhone = "0608000003",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("potrebno izabrati resurs", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OwnerCreate_WithResourceNotAllowedForService_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var otherResourceId = await AddExtraResourceAsync(ids.BusinessId, "Stolica 3");

        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = otherResourceId,
            CustomerName = "Owner Wrong Resource",
            CustomerPhone = "0608000004",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nije dozvoljen za izabranu uslugu", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAll_ShouldReturn_ResourceName_WhenAppointmentHasResource()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Lista sa resursom",
            CustomerPhone = "0601200001",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var response = await client.GetAsync($"/api/Appointments?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AppointmentListItemResponse>>();
        Assert.NotNull(body);

        Assert.Contains(body!, x =>
            x.CustomerName == "Lista sa resursom" &&
            x.ResourceId == ids.ResourceId &&
            x.ResourceName == "Stolica 1");
    }

    [Fact]
    public async Task ProposeDelay_WhenDelayedSlotOverlapsAnotherAppointment_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var firstRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Prvi delay termin",
            CustomerPhone = "0601300001",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var firstResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", firstRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(firstCreated);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Blokator delay termina",
            CustomerPhone = "0601300002",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var delayRequest = new ProposeDelayRequest
        {
            AppointmentId = firstCreated!.Id,
            DelayMinutes = 30,
            Message = "Pomeri za pola sata"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/propose-delay", delayRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_conflict", body!.ReasonCode);
        Assert.Contains("appointment_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.True(body.HasAppointmentConflict);
        Assert.False(body.HasResourceConflict);
    }

    [Fact]
    public async Task Reject_WhenAppointmentDoesNotExist_ShouldReturnNotFoundErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new RejectAppointmentRequest
        {
            AppointmentId = 999999,
            Reason = "Ne postoji"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/reject", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task Reject_WhenAppointmentIsAlreadyConfirmed_ShouldReturnBadRequestErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject confirmed",
            CustomerPhone = "0601400001",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", request);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var rejectRequest = new RejectAppointmentRequest
        {
            AppointmentId = created!.Id,
            Reason = "Kasno odbijanje"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/reject", rejectRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_pending_approval", body!.ReasonCode);
        Assert.Contains("appointment_not_pending_approval", body.ReasonCodes);
    }
    [Fact]
    public async Task RejectProposal_WhenProposalIsAlreadyAccepted_ShouldReturnBadRequestErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject proposal accepted",
            CustomerPhone = "0601400002",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            Notes = "Za reject proposal"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var proposeRequest = new ProposeAppointmentTimeRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Pomeri na 11h"
        };

        var proposeResponse = await client.PostAsJsonAsync("/api/Appointments/propose-time", proposeRequest);
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var proposed = await proposeResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(proposed);
        Assert.True(proposed!.ChangeRequestId.HasValue);

        var acceptRequest = new AcceptAppointmentProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = proposed.ChangeRequestId.Value
        };

        var acceptResponse = await client.PostAsJsonAsync("/api/Appointments/accept-proposal", acceptRequest);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var rejectRequest = new RejectAppointmentProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = proposed.ChangeRequestId.Value,
            Reason = "Prekasno"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/reject-proposal", rejectRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("counter_proposal_accepted", body!.ReasonCode);
        Assert.Contains("counter_proposal_accepted", body.ReasonCodes);
    }

    [Fact]
    public async Task RejectDelay_WhenDelayProposalIsAlreadyAccepted_ShouldReturnBadRequestErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject delay accepted",
            CustomerPhone = "0601400003",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var delayRequest = new ProposeDelayRequest
        {
            AppointmentId = created!.Id,
            DelayMinutes = 30,
            Message = "Kasnimo malo"
        };

        var delayResponse = await client.PostAsJsonAsync("/api/Appointments/propose-delay", delayRequest);
        Assert.Equal(HttpStatusCode.OK, delayResponse.StatusCode);

        var delayed = await delayResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(delayed);
        Assert.True(delayed!.ChangeRequestId.HasValue);

        var acceptDelayRequest = new AcceptDelayProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = delayed.ChangeRequestId.Value
        };

        var acceptDelayResponse = await client.PostAsJsonAsync("/api/Appointments/accept-delay", acceptDelayRequest);
        Assert.Equal(HttpStatusCode.OK, acceptDelayResponse.StatusCode);

        var rejectDelayRequest = new RejectDelayProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = delayed.ChangeRequestId.Value,
            Reason = "Prekasno"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/reject-delay", rejectDelayRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("delay_proposal_accepted", body!.ReasonCode);
        Assert.Contains("delay_proposal_accepted", body.ReasonCodes);
    }

    [Fact]
    public async Task MarkCallAttemptScheduled_WithPastTime_ShouldReturnBadRequestErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Past call attempt",
            CustomerPhone = "0601400004",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
            Notes = "Zakazi poziv"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var request = new ScheduleCallAttemptRequest
        {
            AppointmentId = created!.Id,
            ScheduledAtUtc = DateTime.UtcNow.AddMinutes(-5),
            Note = "Prošlo vreme"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/mark-call-attempt-scheduled", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("scheduled_call_must_be_future", body!.ReasonCode);
        Assert.Contains("scheduled_call_must_be_future", body.ReasonCodes);
    }
    [Fact]
    public async Task MarkNoAnswer_WhenAppointmentIsRejected_ShouldReturnBadRequestErrorResponse()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Rejected before no-answer",
            CustomerPhone = "0601400005",
            StartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            Notes = "Biće odbijen"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var rejectRequest = new RejectAppointmentRequest
        {
            AppointmentId = created!.Id,
            Reason = "Odbijen"
        };

        var rejectResponse = await client.PostAsJsonAsync("/api/Appointments/reject", rejectRequest);
        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var noAnswerRequest = new MarkAppointmentCallActionRequest
        {
            AppointmentId = created.Id,
            Note = "Kasno no-answer"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/mark-no-answer", noAnswerRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_inactive_for_owner_action", body!.ReasonCode);
        Assert.Contains("appointment_inactive_for_owner_action", body.ReasonCodes);
    }

    [Fact]
    public async Task GetAll_WithoutToken_ShouldReturnUnauthorized()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/Appointments?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff read test",
            CustomerPhone = "0601500001",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var response = await staffClient.GetAsync($"/api/Appointments?businessId={ids.BusinessId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AppointmentListItemResponse>>();
        Assert.NotNull(body);
        Assert.Contains(body!, x => x.CustomerName == "Staff read test");
    }

    [Fact]
    public async Task OwnerCreate_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Forbidden staff create",
            CustomerPhone = "0601500002",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/owner-create", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Approve_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff cannot approve",
            CustomerPhone = "0601500003",
            StartAtUtc = new DateTime(2026, 4, 13, 16, 0, 0, DateTimeKind.Utc),
            Notes = "Pending"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/approve", new ApproveAppointmentRequest
        {
            AppointmentId = created!.Id,
            FinalDurationMin = 30
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkCallCustomer_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff call workflow",
            CustomerPhone = "0601500004",
            StartAtUtc = new DateTime(2026, 4, 13, 16, 30, 0, DateTimeKind.Utc),
            Notes = "Call workflow"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/mark-call-customer", new MarkCallCustomerRequest
        {
            AppointmentId = created!.Id,
            Note = "Staff sme call workflow"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCreate_WithManagerMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var managerClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Manager");

        var request = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Manager create test",
            CustomerPhone = "0601500005",
            StartAtUtc = new DateTime(2026, 4, 13, 16, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await managerClient.PostAsJsonAsync("/api/Appointments/owner-create", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    [Fact]
    public async Task Complete_ConfirmedAppointment_AsOwner_ShouldReturnOk_AndCompleted()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Complete owner test",
            CustomerPhone = "0601600001",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdAppointment = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(createdAppointment);

        var response = await client.PostAsJsonAsync("/api/Appointments/complete", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = createdAppointment!.Id,
            Note = "Odradjeno uredno"
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);

        var body = System.Text.Json.JsonSerializer.Deserialize<AppointmentActionResponse>(
            responseBody,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(body);
        Assert.Equal(createdAppointment.Id, body!.AppointmentId);
        Assert.Equal("Completed", body.AppointmentStatus);
        Assert.Equal("Completed", body.Action);
    }

    [Fact]
    public async Task NoShow_ConfirmedAppointment_AsOwner_ShouldReturnOk_AndNoShow()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "NoShow owner test",
            CustomerPhone = "0601600002",
            StartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdAppointment = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(createdAppointment);

        var response = await client.PostAsJsonAsync("/api/Appointments/no-show", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = createdAppointment!.Id,
            Note = "Klijent se nije pojavio"
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);

        var body = System.Text.Json.JsonSerializer.Deserialize<AppointmentActionResponse>(
            responseBody,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(body);
        Assert.Equal(createdAppointment.Id, body!.AppointmentId);
        Assert.Equal("NoShow", body.AppointmentStatus);
        Assert.Equal("NoShow", body.Action);
    }

    [Fact]
    public async Task Cancel_ConfirmedAppointment_AsOwner_ShouldReturnOk_AndCancelled()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Cancel owner test",
            CustomerPhone = "0601600003",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createdAppointment = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(createdAppointment);

        var response = await client.PostAsJsonAsync("/api/Appointments/cancel", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = createdAppointment!.Id,
            Note = "Otkazano od strane radnje"
        });

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);

        var body = System.Text.Json.JsonSerializer.Deserialize<AppointmentActionResponse>(
            responseBody,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(body);
        Assert.Equal(createdAppointment.Id, body!.AppointmentId);
        Assert.Equal("Cancelled", body.AppointmentStatus);
        Assert.Equal("Cancelled", body.Action);
    }

    [Fact]
    public async Task Complete_PendingAppointment_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var publicClient = _factory.CreateClient();

        var createResponse = await publicClient.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Pending complete test",
            CustomerPhone = "0601600004",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 30, 0, DateTimeKind.Utc),
            Notes = "Pending"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await ownerClient.PostAsJsonAsync("/api/Appointments/complete", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Ne bi smelo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_confirmed", body!.ReasonCode);
        Assert.Contains("appointment_not_confirmed", body.ReasonCodes);
    }

    [Fact]
    public async Task NoShow_PendingAppointment_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var publicClient = _factory.CreateClient();

        var createResponse = await publicClient.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Pending no-show test",
            CustomerPhone = "0601600005",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 30, 0, DateTimeKind.Utc),
            Notes = "Pending"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await ownerClient.PostAsJsonAsync("/api/Appointments/no-show", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Ne bi smelo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_confirmed", body!.ReasonCode);
        Assert.Contains("appointment_not_confirmed", body.ReasonCodes);
    }

    [Fact]
    public async Task Cancel_PendingAppointment_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var publicClient = _factory.CreateClient();

        var createResponse = await publicClient.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Pending cancel test",
            CustomerPhone = "0601600006",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc),
            Notes = "Pending"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await ownerClient.PostAsJsonAsync("/api/Appointments/cancel", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Ne bi smelo"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_confirmed", body!.ReasonCode);
        Assert.Contains("appointment_not_confirmed", body.ReasonCodes);
    }

    [Fact]
    public async Task Complete_NotFound_ShouldReturnNotFound()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var response = await client.PostAsJsonAsync("/api/Appointments/complete", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = 999999,
            Note = "Odradjeno uredno"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task NoShow_NotFound_ShouldReturnNotFound()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var response = await client.PostAsJsonAsync("/api/Appointments/no-show", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = 999999,
            Note = "Klijent se nije pojavio"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task Cancel_NotFound_ShouldReturnNotFound()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var response = await client.PostAsJsonAsync("/api/Appointments/cancel", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = 999999,
            Note = "Otkazano od strane radnje"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task Complete_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff complete test",
            CustomerPhone = "0601600007",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/complete", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Staff sme complete"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentActionResponse>();
        Assert.NotNull(body);
        Assert.Equal("Completed", body!.AppointmentStatus);
    }

    [Fact]
    public async Task NoShow_WithStaffMembership_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff no-show test",
            CustomerPhone = "0601600008",
            StartAtUtc = new DateTime(2026, 4, 13, 14, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/no-show", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Staff sme no-show"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentActionResponse>();
        Assert.NotNull(body);
        Assert.Equal("NoShow", body!.AppointmentStatus);
    }

    [Fact]
    public async Task Cancel_WithStaffMembership_ShouldReturnForbidden()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);
        var staffClient = await CreateBusinessMemberClientAsync(ownerClient, ids.BusinessId, "Staff");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Staff cancel forbidden test",
            CustomerPhone = "0601600009",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await staffClient.PostAsJsonAsync("/api/Appointments/cancel", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Staff ne sme cancel"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Complete_ShouldWriteAuditLog()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Audit complete test",
            CustomerPhone = "0601600010",
            StartAtUtc = new DateTime(2026, 4, 13, 16, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var completeResponse = await client.PostAsJsonAsync("/api/Appointments/complete", new UpdateConfirmedAppointmentStatusRequest
        {
            AppointmentId = created!.Id,
            Note = "Audit provera"
        });

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var auditResponse = await client.GetAsync($"/api/Appointments/audit-log?appointmentId={created.Id}");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content.ReadFromJsonAsync<List<AppointmentAuditLogItemResponse>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!, x => x.ActionType == "Completed");
    }

    [Fact]
    public async Task CancelConfirmed_WhenAppointmentIsConfirmed_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer cancel confirmed ok",
            CustomerPhone = "0601700001",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Appointments/cancel-confirmed", new CancelConfirmedAppointmentRequest
        {
            AppointmentId = created!.Id,
            Reason = "Klijentu više ne odgovara termin"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentActionResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.AppointmentId);
        Assert.Equal("Cancelled", body.AppointmentStatus);
        Assert.Equal("CancelledByCustomer", body.Action);
        Assert.Equal("Termin je uspešno otkazan.", body.Message);
    }

    [Fact]
    public async Task CancelConfirmed_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Appointments/cancel-confirmed", new CancelConfirmedAppointmentRequest
        {
            AppointmentId = 999999,
            Reason = "Ne postoji"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task CancelConfirmed_WhenAppointmentIsPending_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer cancel pending",
            CustomerPhone = "0601700002",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            Notes = "Pending termin"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync("/api/Appointments/cancel-confirmed", new CancelConfirmedAppointmentRequest
        {
            AppointmentId = created!.Id,
            Reason = "Ne bi smelo za pending"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_confirmed", body!.ReasonCode);
        Assert.Contains("appointment_not_confirmed", body.ReasonCodes);
    }

    [Fact]
    public async Task CancelConfirmed_ShouldWriteAuditLog()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer cancel audit",
            CustomerPhone = "0601700003",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var client = _factory.CreateClient();

        var cancelResponse = await client.PostAsJsonAsync("/api/Appointments/cancel-confirmed", new CancelConfirmedAppointmentRequest
        {
            AppointmentId = created!.Id,
            Reason = "Audit provera customer cancel"
        });

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var auditResponse = await ownerClient.GetAsync($"/api/Appointments/audit-log?appointmentId={created.Id}");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content.ReadFromJsonAsync<List<AppointmentAuditLogItemResponse>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!, x => x.ActionType == "CancelledByCustomer");
    }

}