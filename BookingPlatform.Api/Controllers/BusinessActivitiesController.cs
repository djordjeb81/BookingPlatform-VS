using BookingPlatform.Contracts.BusinessActivities;
using BookingPlatform.Contracts.Common;
using BookingPlatform.Domain.Appointments;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookingPlatform.Domain.Chat;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status403Forbidden)]
[Route("api/BusinessActivities")]
public sealed class BusinessActivitiesController : ApiControllerBase
{
    public BusinessActivitiesController(BookingDbContext dbContext)
        : base(dbContext)
    {
    }

    [HttpGet("{businessId:long}/summary")]
    [ProducesResponseType(typeof(BusinessActivitySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BusinessActivitySummaryDto>> GetSummary(
        [FromRoute] long businessId,
        CancellationToken cancellationToken)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        await ExpirePendingRequestsAsync(cancellationToken);

        var items = await BuildBusinessActivityItemsAsync(
            businessId,
            take: 10,
            cancellationToken);

        return Ok(new BusinessActivitySummaryDto
        {
            BusinessId = businessId,
            UnreadCount = items.Count(x => x.IsUnread),
            LatestActivity = items.FirstOrDefault(),
            LatestItems = items
        });
    }

    [HttpGet("{businessId:long}/latest")]
    [ProducesResponseType(typeof(List<BusinessActivityItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BusinessActivityItemDto>>> GetLatest(
        [FromRoute] long businessId,
        [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await EnsureBusinessReadAccessAsync(businessId, cancellationToken);
        if (accessResult is not null)
            return accessResult;

        await ExpirePendingRequestsAsync(cancellationToken);

        take = Math.Clamp(take, 1, 50);

        var items = await BuildBusinessActivityItemsAsync(
            businessId,
            take,
            cancellationToken);

        return Ok(items);
    }

    private async Task<List<BusinessActivityItemDto>> BuildBusinessActivityItemsAsync(
        long businessId,
        int take,
        CancellationToken cancellationToken)
    {
        var appointments = await DbContext.Appointments
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                (x.Status == AppointmentStatus.PendingApproval ||
                 x.Status == AppointmentStatus.Confirmed))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);



        var appointmentIds = appointments
            .Select(x => x.Id)
            .ToList();

        var pendingChangeRequests = await DbContext.AppointmentChangeRequests
            .AsNoTracking()
            .Where(x =>
                appointmentIds.Contains(x.AppointmentId) &&
                x.Status == AppointmentChangeRequestStatus.Pending)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var serviceIds = appointments
            .Select(x => x.ServiceId)
            .Distinct()
            .ToList();

        var services = await DbContext.Services
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && serviceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var staffIds = appointments
            .Where(x => x.PrimaryStaffMemberId.HasValue)
            .Select(x => x.PrimaryStaffMemberId!.Value)
            .Distinct()
            .ToList();

        var staff = await DbContext.StaffMembers
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && staffIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

        var resourceIds = appointments
            .Where(x => x.ResourceId.HasValue)
            .Select(x => x.ResourceId!.Value)
            .Distinct()
            .ToList();

        var resources = await DbContext.Resources
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && resourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        var appointmentsById = appointments.ToDictionary(x => x.Id, x => x);

        var result = new List<BusinessActivityItemDto>();

        foreach (var appointment in appointments)
        {
            if (appointment.Status == AppointmentStatus.PendingApproval)
            {
                services.TryGetValue(appointment.ServiceId, out var serviceName);

                string? staffDisplayName = null;
                if (appointment.PrimaryStaffMemberId.HasValue)
                    staff.TryGetValue(appointment.PrimaryStaffMemberId.Value, out staffDisplayName);

                string? resourceName = null;
                if (appointment.ResourceId.HasValue)
                    resources.TryGetValue(appointment.ResourceId.Value, out resourceName);

                result.Add(new BusinessActivityItemDto
                {
                    ActivityType = "NewAppointmentRequest",
                    ActivityTypeLabel = "Novi zahtev za termin",
                    BusinessId = appointment.BusinessId,
                    AppointmentId = appointment.Id,
                    BusinessCustomerId = appointment.BusinessCustomerId,
                    CustomerName = appointment.CustomerName,
                    CustomerPhone = appointment.CustomerPhone,
                    ServiceId = appointment.ServiceId,
                    ServiceName = serviceName,
                    StaffMemberId = appointment.PrimaryStaffMemberId,
                    StaffDisplayName = staffDisplayName,
                    ResourceId = appointment.ResourceId,
                    ResourceName = resourceName,
                    Title = "Novi zahtev za termin",
                    PreviewText = BuildNewAppointmentPreview(appointment.CustomerName, serviceName),
                    CreatedAtUtc = appointment.CreatedAtUtc,
                    StartAtUtc = appointment.StartAtUtc,
                    EndAtUtc = appointment.EndAtUtc,
                    IsUnread = true,
                    RequiresAction = true,
                    PrimaryAction = "OpenRequest"
                });
            }
        }

        foreach (var changeRequest in pendingChangeRequests)
        {
            if (!appointmentsById.TryGetValue(changeRequest.AppointmentId, out var appointment))
                continue;

            if (changeRequest.RequestType == AppointmentChangeRequestType.NewBookingRequest &&
                appointment.Status == AppointmentStatus.PendingApproval)
            {
                continue;
            }

            services.TryGetValue(appointment.ServiceId, out var serviceName);

            string? staffDisplayName = null;
            if (appointment.PrimaryStaffMemberId.HasValue)
                staff.TryGetValue(appointment.PrimaryStaffMemberId.Value, out staffDisplayName);

            string? resourceName = null;
            if (appointment.ResourceId.HasValue)
                resources.TryGetValue(appointment.ResourceId.Value, out resourceName);

            var label = GetChangeRequestLabel(changeRequest.RequestType);
            var preview = BuildChangeRequestPreview(
                appointment.CustomerName,
                serviceName,
                changeRequest);

            result.Add(new BusinessActivityItemDto
            {
                ActivityType = "AppointmentChangeRequest",
                ActivityTypeLabel = label,
                BusinessId = appointment.BusinessId,
                AppointmentId = appointment.Id,
                ChangeRequestId = changeRequest.Id,
                BusinessCustomerId = appointment.BusinessCustomerId,
                CustomerName = appointment.CustomerName,
                CustomerPhone = appointment.CustomerPhone,
                ServiceId = appointment.ServiceId,
                ServiceName = serviceName,
                StaffMemberId = appointment.PrimaryStaffMemberId,
                StaffDisplayName = staffDisplayName,
                ResourceId = appointment.ResourceId,
                ResourceName = resourceName,
                Title = label,
                PreviewText = preview,
                CreatedAtUtc = changeRequest.CreatedAtUtc,
                StartAtUtc = appointment.StartAtUtc,
                EndAtUtc = appointment.EndAtUtc,
                ProposedStartAtUtc = changeRequest.ProposedStartAtUtc,
                ProposedEndAtUtc = changeRequest.ProposedEndAtUtc,
                ExpiresAtUtc = changeRequest.ExpiresAtUtc,
                IsUnread = true,
                RequiresAction = true,
                PrimaryAction = "OpenRequest"
            });
        }

        var unreadChatConversations = await DbContext.ChatConversations
    .AsNoTracking()
    .Where(x =>
        x.BusinessId == businessId &&
        x.IsActive &&
        x.UnreadForBusinessCount > 0)
    .OrderByDescending(x => x.LastMessageAtUtc ?? x.UpdatedAtUtc)
    .Take(50)
    .ToListAsync(cancellationToken);

        var chatBusinessCustomerIds = unreadChatConversations
            .Where(x => x.BusinessCustomerId.HasValue)
            .Select(x => x.BusinessCustomerId!.Value)
            .Distinct()
            .ToList();

        var chatCustomers = await DbContext.BusinessCustomers
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                chatBusinessCustomerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var conversation in unreadChatConversations)
        {
            chatCustomers.TryGetValue(conversation.BusinessCustomerId ?? 0, out var customer);

            var lastMessage = await DbContext.ChatMessages
                .AsNoTracking()
                .Where(x =>
                    x.ConversationId == conversation.Id &&
                    x.SenderType == ChatSenderType.Customer &&
                    x.ReadByBusinessAtUtc == null)
                .OrderByDescending(x => x.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            result.Add(new BusinessActivityItemDto
            {
                ActivityType = "ChatMessage",
                ActivityTypeLabel = "Nova poruka",
                BusinessId = conversation.BusinessId,
                BusinessCustomerId = conversation.BusinessCustomerId,
                ConversationId = conversation.Id,
                ChatMessageId = lastMessage?.Id,
                CustomerName = customer?.FullName ?? "Klijent",
                CustomerPhone = customer?.Phone,
                Title = "Nova poruka",
                PreviewText = conversation.LastMessageText ?? lastMessage?.Text ?? "Nova poruka od klijenta.",
                CreatedAtUtc = conversation.LastMessageAtUtc ?? lastMessage?.CreatedAtUtc ?? conversation.UpdatedAtUtc,
                IsUnread = true,
                RequiresAction = true,
                PrimaryAction = "OpenChat"
            });
        }

        return result
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToList();
    }

    private async Task ExpirePendingRequestsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var expiredRequests = await DbContext.AppointmentChangeRequests
            .Where(x =>
                x.Status == AppointmentChangeRequestStatus.Pending &&
                x.ExpiresAtUtc.HasValue &&
                x.ExpiresAtUtc.Value <= now)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (expiredRequests.Count == 0)
            return;

        var appointmentIds = expiredRequests
            .Select(x => x.AppointmentId)
            .Distinct()
            .ToList();

        var appointments = await DbContext.Appointments
            .Where(x => appointmentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        foreach (var changeRequest in expiredRequests)
        {
            if (!appointments.TryGetValue(changeRequest.AppointmentId, out var appointment))
                continue;

            changeRequest.Status = AppointmentChangeRequestStatus.Expired;
            changeRequest.RespondedAtUtc = now;
            changeRequest.UpdatedAtUtc = now;

            string actionType;
            string message;
            string? oldValuesJson = null;
            string? newValuesJson = null;

            switch (changeRequest.RequestType)
            {
                case AppointmentChangeRequestType.NewBookingRequest:
                    appointment.Status = AppointmentStatus.Rejected;
                    appointment.UpdatedAtUtc = now;

                    actionType = "NewBookingRequestExpired";
                    message = "Zahtev za termin je istekao jer nije bilo odgovora na vreme.";
                    newValuesJson = $"Status={appointment.Status}";
                    break;

                case AppointmentChangeRequestType.CounterProposal:
                    appointment.Status = AppointmentStatus.Rejected;
                    appointment.UpdatedAtUtc = now;

                    actionType = "CounterProposalExpired";
                    message = "Predlog novog termina je istekao jer nije bilo odgovora na vreme.";
                    newValuesJson = $"Status={appointment.Status}";
                    break;

                case AppointmentChangeRequestType.DelayProposal:
                    actionType = "DelayProposalExpired";
                    message = "Predlog pomeranja je istekao, pa ostaje prethodno potvrđeni termin.";
                    oldValuesJson = $"ProposedStart={changeRequest.ProposedStartAtUtc:o};ProposedEnd={changeRequest.ProposedEndAtUtc:o}";
                    newValuesJson = $"Start={appointment.StartAtUtc:o};End={appointment.EndAtUtc:o};Status={appointment.Status}";
                    break;

                default:
                    actionType = "ChangeRequestExpired";
                    message = "Zahtev više nije aktivan jer je istekao.";
                    break;
            }

            DbContext.AppointmentAuditLogs.Add(new AppointmentAuditLog
            {
                AppointmentId = appointment.Id,
                ActionType = actionType,
                Message = message,
                OldValuesJson = oldValuesJson,
                NewValuesJson = newValuesJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildNewAppointmentPreview(
        string customerName,
        string? serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
            return $"{customerName} šalje novi zahtev za termin.";

        return $"{customerName} šalje novi zahtev za termin: {serviceName}.";
    }

    private static string BuildChangeRequestPreview(
        string customerName,
        string? serviceName,
        AppointmentChangeRequest changeRequest)
    {
        var servicePart = string.IsNullOrWhiteSpace(serviceName)
            ? "termin"
            : serviceName;

        return changeRequest.RequestType switch
        {
            AppointmentChangeRequestType.RescheduleRequest =>
                $"{customerName} traži promenu termina za: {servicePart}.",

            AppointmentChangeRequestType.CounterProposal =>
                $"Potrebno je odgovoriti na predlog termina za: {servicePart}.",

            AppointmentChangeRequestType.DelayProposal =>
                $"Potrebno je odgovoriti na predlog pomeranja za: {servicePart}.",

            AppointmentChangeRequestType.NewBookingRequest =>
                $"{customerName} šalje novi zahtev za termin: {servicePart}.",

            _ =>
                $"Potrebno je odgovoriti na zahtev za: {servicePart}."
        };
    }

    private static string GetChangeRequestLabel(AppointmentChangeRequestType requestType)
    {
        return requestType switch
        {
            AppointmentChangeRequestType.NewBookingRequest => "Novi zahtev za termin",
            AppointmentChangeRequestType.RescheduleRequest => "Zahtev za promenu termina",
            AppointmentChangeRequestType.CounterProposal => "Predlog novog termina",
            AppointmentChangeRequestType.DelayProposal => "Predlog pomeranja termina",
            _ => "Zahtev za termin"
        };
    }
}