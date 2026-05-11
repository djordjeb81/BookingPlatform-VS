using System.Net;
using System.Net.Http.Json;
using BookingPlatform.Contracts.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using BookingPlatform.Domain.Scheduling;
using BookingPlatform.Domain.Staff;
using System.Net.Http.Headers;
using BookingPlatform.Contracts.Auth;

namespace BookingPlatform.Tests.Integration;

[Collection("Integration collection")]
public sealed class AppointmentInboxWorkflowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AppointmentInboxWorkflowTests(CustomWebApplicationFactory factory)
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

    [Fact]
    public async Task Inbox_ShouldContain_NewPendingBookingRequest()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Inbox Test",
            CustomerPhone = "0601000001",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            Notes = "Nova rezervacija"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var response = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(body);

        Assert.Contains(body!, x =>
            x.CustomerName == "Inbox Test" &&
            x.AppointmentStatus == "PendingApproval" &&
            x.ChangeRequestType == "NewBookingRequest" &&
            x.OwnerWorkflowState == "pending_business_approval");
    }

    [Fact]
    public async Task Approve_ShouldRemoveAppointmentFromInbox()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Approve Inbox",
            CustomerPhone = "0601000002",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc),
            Notes = "Za approve"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var approveRequest = new ApproveAppointmentRequest
        {
            AppointmentId = created!.Id
        };

        var approveResponse = await client.PostAsJsonAsync("/api/Appointments/approve", approveRequest);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var inboxResponse = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);

        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inbox);

        Assert.DoesNotContain(inbox!, x => x.AppointmentId == created.Id);
    }

    [Fact]
    public async Task ProposeTime_ShouldAppearInInbox_AsWaitingCustomerForCounterProposal()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Counter Proposal",
            CustomerPhone = "0601000003",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            Notes = "Za counter"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var proposeRequest = new ProposeAppointmentTimeRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Može u 11h"
        };

        var proposeResponse = await client.PostAsJsonAsync("/api/Appointments/propose-time", proposeRequest);
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var inboxResponse = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);

        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inbox);

        Assert.Contains(inbox!, x =>
            x.AppointmentId == created.Id &&
            x.ChangeRequestType == "CounterProposal" &&
            x.OwnerWorkflowState == "waiting_customer_for_counter_proposal");
    }

    [Fact]
    public async Task ProposeDelay_ShouldAppearInInbox_AsWaitingCustomerForDelayProposal()
    {
        var ids = await SeedAsync();

        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Delay Inbox",
            CustomerPhone = "0601000004",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();

        Assert.NotNull(created);

        var delayRequest = new ProposeDelayRequest
        {
            AppointmentId = created!.Id,
            DelayMinutes = 15,
            Message = "Kasnimo malo"
        };

        var delayResponse = await client.PostAsJsonAsync("/api/Appointments/propose-delay", delayRequest);
        Assert.Equal(HttpStatusCode.OK, delayResponse.StatusCode);

        var inboxResponse = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);

        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inbox);

        Assert.Contains(inbox!, x =>
            x.AppointmentId == created.Id &&
            x.ChangeRequestType == "DelayProposal" &&
            x.OwnerWorkflowState == "waiting_customer_for_delay_proposal");
    }

    [Fact]
    public async Task MarkNoAnswer_OnPendingAppointment_ShouldKeepItemInInboxWithUpdatedWorkflowState()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "No Answer",
            CustomerPhone = "0601000005",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
            Notes = "Treba pozvati"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var noAnswerRequest = new MarkAppointmentCallActionRequest
        {
            AppointmentId = created!.Id,
            Note = "Nije se javio"
        };

        var noAnswerResponse = await client.PostAsJsonAsync("/api/Appointments/mark-no-answer", noAnswerRequest);
        Assert.Equal(HttpStatusCode.OK, noAnswerResponse.StatusCode);

        var inboxResponse = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);

        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inbox);

        Assert.Contains(inbox!, x =>
            x.AppointmentId == created.Id &&
            x.OwnerWorkflowState == "pending_business_approval" &&
            x.LastOwnerAction == "CustomerCallNoAnswer");
    }

    [Fact]
    public async Task ChangeRequests_ShouldReturnHistory_ForAppointment()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "History Test",
            CustomerPhone = "0601000006",
            StartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            Notes = "Istorija"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();

        Assert.NotNull(created);

        var historyResponse = await client.GetAsync($"/api/Appointments/change-requests?appointmentId={created!.Id}");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<AppointmentChangeRequestItemResponse>>();
        Assert.NotNull(history);
        Assert.NotEmpty(history!);

        Assert.Contains(history!, x =>
            x.AppointmentId == created.Id &&
            x.RequestType == "NewBookingRequest" &&
            x.Status == "Pending");
    }

    [Fact]
    public async Task CustomerCreate_WithOverlappingSameResourceOnDifferentStaff_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Blokiraj resource",
            CustomerPhone = "0609000001",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var conflictingRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "Resource konflikt",
            CustomerPhone = "0609000002",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 0, 0, DateTimeKind.Utc),
            Notes = "Isti resource, drugi zaposleni"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments", conflictingRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAppointmentErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.True(body.HasResourceConflict);
        Assert.False(body.HasAppointmentConflict);
    }

    [Fact]
    public async Task OwnerCreate_WithOverlappingSameResourceOnDifferentStaff_ShouldReturnBadRequestWithResourceConflict()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Prvi termin",
            CustomerPhone = "0609000010",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var conflictingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "Drugi termin",
            CustomerPhone = "0609000011",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/owner-create", conflictingRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<OwnerCreateAvailabilityErrorResponse>();
        Assert.NotNull(body);
        Assert.True(body!.HasResourceConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.Equal("resource_conflict", body.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
    }

    [Fact]
    public async Task ProposeTime_IntoBusyResourceIntervalOnDifferentStaff_ShouldReturnBadRequest()
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
            CustomerName = "Blokator",
            CustomerPhone = "0609000020",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Za propose resource",
            CustomerPhone = "0609000021",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            Notes = "Početni termin"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var proposeRequest = new ProposeAppointmentTimeRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Upada u zauzet resource"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/propose-time", proposeRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.True(body.HasResourceConflict);
    }

    [Fact]
    public async Task Approve_WithFinalDurationThatOverlapsBusyResourceOnDifferentStaff_ShouldReturnBadRequest()
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
            CustomerName = "Blokator approve",
            CustomerPhone = "0609000030",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Approve resource",
            CustomerPhone = "0609000031",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            Notes = "Čeka approve"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var approveRequest = new ApproveAppointmentRequest
        {
            AppointmentId = created!.Id,
            FinalDurationMin = 60
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/approve", approveRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.True(body.HasResourceConflict);
    }

    [Fact]
    public async Task MarkCalledConfirmed_WithFinalDurationThatOverlapsBusyResourceOnDifferentStaff_ShouldReturnBadRequest()
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
            CustomerName = "Blokator call confirm",
            CustomerPhone = "0609000040",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Call confirm resource",
            CustomerPhone = "0609000041",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
            Notes = "Čeka telefonsku potvrdu"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", createRequest);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var confirmRequest = new MarkAppointmentCallActionRequest
        {
            AppointmentId = created!.Id,
            FinalDurationMin = 60,
            Note = "Probaj produženje"
        };

        var response = await client.PostAsJsonAsync("/api/Appointments/mark-called-confirmed", confirmRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.True(body.HasResourceConflict);
    }

    [Fact]
    public async Task AcceptProposal_ShouldReturnBadRequest_WhenProposedSlotBecomesUnavailableDueToResourceConflict()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Accept Proposal Test",
            CustomerPhone = "0601100001",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            Notes = "Originalni termin"
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
            Message = "Može u 11h"
        };

        var proposeResponse = await client.PostAsJsonAsync("/api/Appointments/propose-time", proposeRequest);
        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var proposed = await proposeResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(proposed);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "Blokator prihvata predloga",
            CustomerPhone = "0601100002",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        Assert.True(proposed!.ChangeRequestId.HasValue);

        var acceptRequest = new AcceptAppointmentProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = proposed.ChangeRequestId.Value
        };

        var acceptResponse = await client.PostAsJsonAsync("/api/Appointments/accept-proposal", acceptRequest);

        Assert.Equal(HttpStatusCode.BadRequest, acceptResponse.StatusCode);

        var body = await acceptResponse.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.True(body.HasResourceConflict);
    }

    [Fact]
    public async Task AcceptDelay_ShouldReturnBadRequest_WhenDelayedSlotBecomesUnavailableDueToResourceConflict()
    {
        var ids = await SeedAsync();
        var secondStaffId = await AddSecondStaffMemberAsync(ids.BusinessId);
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var createRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Accept Delay Test",
            CustomerPhone = "0601100010",
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
            Message = "Pomeramo za pola sata"
        };

        var delayResponse = await client.PostAsJsonAsync("/api/Appointments/propose-delay", delayRequest);
        Assert.Equal(HttpStatusCode.OK, delayResponse.StatusCode);

        var delayed = await delayResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(delayed);

        var blockingRequest = new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = secondStaffId,
            ResourceId = ids.ResourceId,
            CustomerName = "Blokator prihvata delay-a",
            CustomerPhone = "0601100011",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        };

        var blockingResponse = await client.PostAsJsonAsync("/api/Appointments/owner-create", blockingRequest);
        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        Assert.True(delayed!.ChangeRequestId.HasValue);

        var acceptDelayRequest = new AcceptDelayProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = delayed.ChangeRequestId.Value
        };

        var acceptDelayResponse = await client.PostAsJsonAsync("/api/Appointments/accept-delay", acceptDelayRequest);

        Assert.Equal(HttpStatusCode.BadRequest, acceptDelayResponse.StatusCode);

        var body = await acceptDelayResponse.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("resource_conflict", body!.ReasonCode);
        Assert.Contains("resource_conflict", body.ReasonCodes);
        Assert.False(body.HasSlotGridViolation);
        Assert.False(body.HasBusinessHoursViolation);
        Assert.False(body.HasStaffHoursViolation);
        Assert.False(body.HasTimeOffConflict);
        Assert.False(body.HasAppointmentConflict);
        Assert.True(body.HasResourceConflict);
    }

    [Fact]
    public async Task Inbox_ShouldReturn_ResourceName_WhenAppointmentHasResource()
    {
        var ids = await SeedAsync();
        var client = await CreateOwnerClientAsync(ids.BusinessId);

        var request = new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            ResourceId = ids.ResourceId,
            CustomerName = "Inbox resource",
            CustomerPhone = "0601200002",
            StartAtUtc = new DateTime(2026, 4, 13, 9, 30, 0, DateTimeKind.Utc),
            Notes = "Inbox sa resursom"
        };

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", request);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var response = await client.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(body);

        Assert.Contains(body!, x =>
            x.CustomerName == "Inbox resource" &&
            x.ResourceId == ids.ResourceId &&
            x.ResourceName == "Stolica 1");
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
    public async Task RequestReschedule_WhenAppointmentIsConfirmed_ShouldReturnOk()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule confirmed ok",
            CustomerPhone = "0601800001",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
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

        var response = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            Message = "Može li termin malo kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(body);
        Assert.Equal(created.Id, body!.AppointmentId);
        Assert.Equal("Confirmed", body.AppointmentStatus);
        Assert.True(body.ChangeRequestId.HasValue);
        Assert.Equal("Pending", body.ChangeRequestStatus);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc), body.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc), body.EndAtUtc);
        Assert.Equal("Zahtev za promenu termina je uspešno poslat.", body.Message);
    }

    [Fact]
    public async Task RequestReschedule_WhenAppointmentDoesNotExist_ShouldReturnNotFound()
    {
        await SeedAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = 999999,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            Message = "Ne postoji"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_found", body!.ReasonCode);
        Assert.Contains("appointment_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task RequestReschedule_WhenAppointmentIsPending_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var client = _factory.CreateClient();

        var createResponse = await client.PostAsJsonAsync("/api/Appointments", new CreateAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule pending",
            CustomerPhone = "0601800002",
            StartAtUtc = new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc),
            Notes = "Pending termin"
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<CreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
            Message = "Ne bi smelo za pending"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_not_confirmed", body!.ReasonCode);
        Assert.Contains("appointment_not_confirmed", body.ReasonCodes);
    }

    [Fact]
    public async Task RequestReschedule_WhenProposedStartIsOutsideGrid_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule outside grid",
            CustomerPhone = "0601800003",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc),
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

        var response = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 13, 10, 0, DateTimeKind.Utc),
            Message = "Van grid-a"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("outside_slot_grid", body!.ReasonCode);
        Assert.Contains("outside_slot_grid", body.ReasonCodes);
        Assert.True(body.HasSlotGridViolation);
    }

    [Fact]
    public async Task RequestReschedule_WhenProposedTimeConflicts_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var firstResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule source",
            CustomerPhone = "0601800004",
            StartAtUtc = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var firstCreated = await firstResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(firstCreated);

        var blockingResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule blocker",
            CustomerPhone = "0601800005",
            StartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, blockingResponse.StatusCode);

        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = firstCreated!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            Message = "Upada u konflikt"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("appointment_conflict", body!.ReasonCode);
        Assert.Contains("appointment_conflict", body.ReasonCodes);
        Assert.True(body.HasAppointmentConflict);
    }

    [Fact]
    public async Task RequestReschedule_ShouldWriteAuditLog()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Customer reschedule audit",
            CustomerPhone = "0601800006",
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

        var client = _factory.CreateClient();

        var rescheduleResponse = await client.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
            Message = "Audit provera customer reschedule"
        });

        Assert.Equal(HttpStatusCode.OK, rescheduleResponse.StatusCode);

        var auditResponse = await ownerClient.GetAsync($"/api/Appointments/audit-log?appointmentId={created.Id}");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);

        var audit = await auditResponse.Content.ReadFromJsonAsync<List<AppointmentAuditLogItemResponse>>();
        Assert.NotNull(audit);
        Assert.Contains(audit!, x => x.ActionType == "RescheduleRequestedByCustomer");
    }

    [Fact]
    public async Task AcceptRescheduleRequest_ShouldUpdateAppointment_AndRemovePendingItemFromInbox()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Accept reschedule inbox",
            CustomerPhone = "0601900001",
            StartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var publicClient = _factory.CreateClient();

        var requestResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            Message = "Može kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var requested = await requestResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(requested);
        Assert.True(requested!.ChangeRequestId.HasValue);

        var inboxBeforeResponse = await ownerClient.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxBeforeResponse.StatusCode);

        var inboxBefore = await inboxBeforeResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inboxBefore);

        Assert.Contains(inboxBefore!, x =>
            x.AppointmentId == created.Id &&
            x.ChangeRequestType == "RescheduleRequest" &&
            x.ChangeRequestStatus == "Pending");

        var acceptResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/accept-reschedule-request", new AcceptRescheduleRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = requested.ChangeRequestId.Value
        });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var accepted = await acceptResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(accepted);
        Assert.Equal("Accepted", accepted!.ChangeRequestStatus);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc), accepted.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc), accepted.EndAtUtc);

        var appointmentsResponse = await ownerClient.GetAsync($"/api/Appointments?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, appointmentsResponse.StatusCode);

        var appointments = await appointmentsResponse.Content.ReadFromJsonAsync<List<AppointmentListItemResponse>>();
        Assert.NotNull(appointments);

        Assert.Contains(appointments!, x =>
            x.Id == created.Id &&
            x.Status == "Confirmed" &&
            x.StartAtUtc == new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc) &&
            x.EndAtUtc == new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc));

        var inboxAfterResponse = await ownerClient.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxAfterResponse.StatusCode);

        var inboxAfter = await inboxAfterResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inboxAfter);

        Assert.DoesNotContain(inboxAfter!, x => x.AppointmentId == created.Id);
    }

    [Fact]
    public async Task RejectRescheduleRequest_ShouldKeepOriginalAppointment_AndRemovePendingItemFromInbox()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var originalStart = new DateTime(2026, 4, 13, 13, 0, 0, DateTimeKind.Utc);
        var originalEnd = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject reschedule inbox",
            CustomerPhone = "0601900002",
            StartAtUtc = originalStart,
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var publicClient = _factory.CreateClient();

        var requestResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            Message = "Može sat kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var requested = await requestResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(requested);
        Assert.True(requested!.ChangeRequestId.HasValue);

        var rejectResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/reject-reschedule-request", new RejectRescheduleRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = requested.ChangeRequestId.Value,
            Reason = "Ne odgovara rasporedu radnje."
        });

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var rejected = await rejectResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected!.ChangeRequestStatus);
        Assert.Equal("Confirmed", rejected.AppointmentStatus);

        var appointmentsResponse = await ownerClient.GetAsync($"/api/Appointments?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, appointmentsResponse.StatusCode);

        var appointments = await appointmentsResponse.Content.ReadFromJsonAsync<List<AppointmentListItemResponse>>();
        Assert.NotNull(appointments);

        Assert.Contains(appointments!, x =>
            x.Id == created.Id &&
            x.Status == "Confirmed" &&
            x.StartAtUtc == originalStart &&
            x.EndAtUtc == originalEnd);

        var inboxAfterResponse = await ownerClient.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxAfterResponse.StatusCode);

        var inboxAfter = await inboxAfterResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inboxAfter);

        Assert.DoesNotContain(inboxAfter!, x => x.AppointmentId == created.Id);
    }

    [Fact]
    public async Task AcceptRescheduleRequest_WhenRequestDoesNotExist_ShouldReturnNotFound()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Accept reschedule not found",
            CustomerPhone = "0601900003",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var response = await ownerClient.PostAsJsonAsync("/api/Appointments/accept-reschedule-request", new AcceptRescheduleRequest
        {
            AppointmentId = created!.Id,
            ChangeRequestId = 999999
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("reschedule_request_not_found", body!.ReasonCode);
        Assert.Contains("reschedule_request_not_found", body.ReasonCodes);
    }

    [Fact]
    public async Task RejectRescheduleRequest_WhenRequestIsAlreadyAccepted_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject accepted reschedule",
            CustomerPhone = "0601900004",
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

        var publicClient = _factory.CreateClient();

        var requestResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 16, 0, 0, DateTimeKind.Utc),
            Message = "Može kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, requestResponse.StatusCode);

        var requested = await requestResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(requested);
        Assert.True(requested!.ChangeRequestId.HasValue);

        var acceptResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/accept-reschedule-request", new AcceptRescheduleRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = requested.ChangeRequestId.Value
        });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var rejectResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/reject-reschedule-request", new RejectRescheduleRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = requested.ChangeRequestId.Value,
            Reason = "Prekasno"
        });

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);

        var body = await rejectResponse.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("reschedule_request_accepted", body!.ReasonCode);
        Assert.Contains("reschedule_request_accepted", body.ReasonCodes);
    }
    [Fact]
    public async Task ProposeTime_ForConfirmedAppointmentWithPendingRescheduleRequest_ShouldCreateCounterProposal()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Counter on reschedule",
            CustomerPhone = "0601950001",
            StartAtUtc = new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var publicClient = _factory.CreateClient();

        var requestRescheduleResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 11, 0, 0, DateTimeKind.Utc),
            Message = "Može u 11h?"
        });

        Assert.Equal(HttpStatusCode.OK, requestRescheduleResponse.StatusCode);

        var rescheduleRequest = await requestRescheduleResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(rescheduleRequest);
        Assert.True(rescheduleRequest!.ChangeRequestId.HasValue);

        var proposeResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/propose-time", new ProposeAppointmentTimeRequest
        {
            AppointmentId = created.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Ne može u 11h, može u 12h."
        });

        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var proposed = await proposeResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(proposed);
        Assert.True(proposed!.ChangeRequestId.HasValue);
        Assert.Equal("Pending", proposed.ChangeRequestStatus);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 0, 0, DateTimeKind.Utc), proposed.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 12, 30, 0, DateTimeKind.Utc), proposed.EndAtUtc);

        var historyResponse = await ownerClient.GetAsync($"/api/Appointments/change-requests?appointmentId={created.Id}");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await historyResponse.Content.ReadFromJsonAsync<List<AppointmentChangeRequestItemResponse>>();
        Assert.NotNull(history);

        Assert.Contains(history!, x =>
            x.Id == rescheduleRequest.ChangeRequestId.Value &&
            x.RequestType == "RescheduleRequest" &&
            x.Status == "Rejected");

        Assert.Contains(history!, x =>
            x.Id == proposed.ChangeRequestId.Value &&
            x.RequestType == "CounterProposal" &&
            x.Status == "Pending");
    }

    [Fact]
    public async Task Inbox_ShouldShowWaitingCustomerForCounterProposal_AfterCounteringRescheduleRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Inbox counter reschedule",
            CustomerPhone = "0601950002",
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

        var publicClient = _factory.CreateClient();

        var requestRescheduleResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 13, 30, 0, DateTimeKind.Utc),
            Message = "Može kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, requestRescheduleResponse.StatusCode);

        var proposeResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/propose-time", new ProposeAppointmentTimeRequest
        {
            AppointmentId = created.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 14, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Može u 14h."
        });

        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var inboxResponse = await ownerClient.GetAsync($"/api/Appointments/inbox?businessId={ids.BusinessId}");
        Assert.Equal(HttpStatusCode.OK, inboxResponse.StatusCode);

        var inbox = await inboxResponse.Content.ReadFromJsonAsync<List<AppointmentInboxItemDto>>();
        Assert.NotNull(inbox);

        Assert.Contains(inbox!, x =>
            x.AppointmentId == created.Id &&
            x.ChangeRequestType == "CounterProposal" &&
            x.ChangeRequestStatus == "Pending" &&
            x.OwnerWorkflowState == "waiting_customer_for_counter_proposal");
    }

    [Fact]
    public async Task RejectRescheduleRequest_AfterOwnerCounterProposal_ShouldReturnBadRequest()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Reject after counter",
            CustomerPhone = "0601950003",
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

        var publicClient = _factory.CreateClient();

        var requestRescheduleResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 15, 30, 0, DateTimeKind.Utc),
            Message = "Može kasnije?"
        });

        Assert.Equal(HttpStatusCode.OK, requestRescheduleResponse.StatusCode);

        var requested = await requestRescheduleResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(requested);
        Assert.True(requested!.ChangeRequestId.HasValue);

        var proposeResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/propose-time", new ProposeAppointmentTimeRequest
        {
            AppointmentId = created.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 16, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Može u 16h."
        });

        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var rejectResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/reject-reschedule-request", new RejectRescheduleRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = requested.ChangeRequestId.Value,
            Reason = "Prekasno"
        });

        Assert.Equal(HttpStatusCode.BadRequest, rejectResponse.StatusCode);

        var body = await rejectResponse.Content.ReadFromJsonAsync<AppointmentOperationErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("reschedule_request_rejected", body!.ReasonCode);
        Assert.Contains("reschedule_request_rejected", body.ReasonCodes);
    }

    [Fact]
    public async Task CustomerAcceptProposal_AfterOwnerCounteredRescheduleRequest_ShouldConfirmNewTime()
    {
        var ids = await SeedAsync();
        var ownerClient = await CreateOwnerClientAsync(ids.BusinessId);

        var createResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/owner-create", new CreateOwnerAppointmentRequest
        {
            BusinessId = ids.BusinessId,
            ServiceId = ids.ServiceId,
            PrimaryStaffMemberId = ids.StaffMemberId,
            CustomerName = "Accept counter after reschedule",
            CustomerPhone = "0601950004",
            StartAtUtc = new DateTime(2026, 4, 13, 15, 0, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            IgnoreAvailabilityRules = false,
            IgnoreWorkingHours = false,
            IgnoreTimeOffBlocks = false,
            IgnoreAppointmentConflicts = false
        });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<OwnerCreateAppointmentResponse>();
        Assert.NotNull(created);

        var publicClient = _factory.CreateClient();

        var requestRescheduleResponse = await publicClient.PostAsJsonAsync("/api/Appointments/request-reschedule", new RequestAppointmentRescheduleRequest
        {
            AppointmentId = created!.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 16, 0, 0, DateTimeKind.Utc),
            Message = "Može u 16h?"
        });

        Assert.Equal(HttpStatusCode.OK, requestRescheduleResponse.StatusCode);

        var requested = await requestRescheduleResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(requested);

        var proposeResponse = await ownerClient.PostAsJsonAsync("/api/Appointments/propose-time", new ProposeAppointmentTimeRequest
        {
            AppointmentId = created.Id,
            ProposedStartAtUtc = new DateTime(2026, 4, 13, 16, 30, 0, DateTimeKind.Utc),
            FinalDurationMin = 30,
            Message = "Može u 16:30."
        });

        Assert.Equal(HttpStatusCode.OK, proposeResponse.StatusCode);

        var proposed = await proposeResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(proposed);
        Assert.True(proposed!.ChangeRequestId.HasValue);

        var acceptResponse = await publicClient.PostAsJsonAsync("/api/Appointments/accept-proposal", new AcceptAppointmentProposalRequest
        {
            AppointmentId = created.Id,
            ChangeRequestId = proposed.ChangeRequestId.Value
        });

        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var accepted = await acceptResponse.Content.ReadFromJsonAsync<AppointmentChangeActionResponse>();
        Assert.NotNull(accepted);
        Assert.Equal("Confirmed", accepted!.AppointmentStatus);
        Assert.Equal(new DateTime(2026, 4, 13, 16, 30, 0, DateTimeKind.Utc), accepted.StartAtUtc);
        Assert.Equal(new DateTime(2026, 4, 13, 17, 0, 0, DateTimeKind.Utc), accepted.EndAtUtc);
    }
}
