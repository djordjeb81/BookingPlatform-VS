using BookingPlatform.Contracts.BusinessActivityNotifications;
using BookingPlatform.Domain.BusinessActivityNotifications;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookingPlatform.Api.Controllers;

[ApiController]
[Route("api/businesses/{businessId:long}/activity-notifications")]
public sealed class BusinessActivityNotificationsController : ControllerBase
{
    private readonly BookingDbContext _db;

    public BusinessActivityNotificationsController(BookingDbContext db)
    {
        _db = db;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BusinessActivityNotificationSummaryDto>> GetSummary(
        long businessId,
        [FromQuery] string? recipientKey = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedRecipientKey = NormalizeRecipientKey(recipientKey);

        var activeQuery = _db.BusinessActivityNotifications
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == normalizedRecipientKey &&
                !x.IsResolved);

        var activeCount = await activeQuery.CountAsync(cancellationToken);

        var snoozedCount = await activeQuery
            .CountAsync(x =>
                x.SnoozedUntilUtc.HasValue &&
                x.SnoozedUntilUtc.Value > nowUtc,
                cancellationToken);

        var dueUnseenQuery = activeQuery
            .Where(x =>
                !x.IsSeen &&
                (!x.SnoozedUntilUtc.HasValue || x.SnoozedUntilUtc.Value <= nowUtc));

        var unseenCount = await dueUnseenQuery.CountAsync(cancellationToken);

        var latestUnseen = await dueUnseenQuery
            .OrderByDescending(x => x.Priority)
            .ThenByDescending(x => x.SortAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);

        var latestItems = await activeQuery
            .OrderBy(x =>
                x.SnoozedUntilUtc.HasValue && x.SnoozedUntilUtc.Value > nowUtc
                    ? 1
                    : 0)
            .ThenBy(x => x.IsSeen ? 1 : 0)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.SortAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(10)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(new BusinessActivityNotificationSummaryDto
        {
            BusinessId = businessId,
            RecipientType = BusinessActivityNotificationRecipients.Business,
            RecipientKey = normalizedRecipientKey,
            ActiveCount = activeCount,
            UnseenCount = unseenCount,
            SnoozedCount = snoozedCount,
            HasUnseen = unseenCount > 0,
            LatestUnseen = latestUnseen,
            LatestItems = latestItems
        });
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessActivityNotificationDto>>> GetNotifications(
        long businessId,
        [FromQuery] string? recipientKey = null,
        [FromQuery] bool includeResolved = false,
        [FromQuery] bool includeSeen = true,
        [FromQuery] bool includeSnoozed = true,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedRecipientKey = NormalizeRecipientKey(recipientKey);

        if (take <= 0)
            take = 100;

        if (take > 300)
            take = 300;

        var query = _db.BusinessActivityNotifications
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == normalizedRecipientKey);

        if (!includeResolved)
        {
            query = query.Where(x => !x.IsResolved);
        }

        if (!includeSeen)
        {
            query = query.Where(x => !x.IsSeen);
        }

        if (!includeSnoozed)
        {
            query = query.Where(x =>
                !x.SnoozedUntilUtc.HasValue ||
                x.SnoozedUntilUtc.Value <= nowUtc);
        }

        var result = await query
            .OrderBy(x =>
                x.SnoozedUntilUtc.HasValue && x.SnoozedUntilUtc.Value > nowUtc
                    ? 1
                    : 0)
            .ThenBy(x => x.IsSeen ? 1 : 0)
            .ThenByDescending(x => x.Priority)
            .ThenByDescending(x => x.SortAtUtc)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost("{notificationId:long}/mark-seen")]
    public async Task<IActionResult> MarkSeen(
        long businessId,
        long notificationId,
        [FromBody] MarkBusinessActivityNotificationSeenRequest? request,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (notification is null)
            return NotFound();

        if (!notification.IsSeen)
        {
            notification.IsSeen = true;
            notification.SeenAtUtc = DateTime.UtcNow;
        }

        notification.SeenByUserId = request?.SeenByUserId;
        notification.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("mark-all-seen")]
    public async Task<IActionResult> MarkAllSeen(
        long businessId,
        [FromQuery] string? recipientKey = null,
        [FromBody] MarkBusinessActivityNotificationSeenRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var normalizedRecipientKey = NormalizeRecipientKey(recipientKey);

        var notifications = await _db.BusinessActivityNotifications
            .Where(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == normalizedRecipientKey &&
                !x.IsResolved &&
                !x.IsSeen)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsSeen = true;
            notification.SeenAtUtc = nowUtc;
            notification.SeenByUserId = request?.SeenByUserId;
            notification.UpdatedAtUtc = nowUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{notificationId:long}/snooze")]
    public async Task<IActionResult> Snooze(
        long businessId,
        long notificationId,
        [FromBody] SnoozeBusinessActivityNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SnoozedUntilUtc <= DateTime.UtcNow)
            return BadRequest("Vreme odlaganja mora biti u budućnosti.");

        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (notification is null)
            return NotFound();

        var nowUtc = DateTime.UtcNow;

        notification.IsSeen = true;
        notification.SeenAtUtc ??= nowUtc;
        notification.SnoozedUntilUtc = request.SnoozedUntilUtc;
        notification.SnoozedByUserId = null;
        notification.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{notificationId:long}/clear-snooze")]
    public async Task<IActionResult> ClearSnooze(
        long businessId,
        long notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (notification is null)
            return NotFound();

        notification.SnoozedUntilUtc = null;
        notification.SnoozedByUserId = null;
        notification.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{notificationId:long}/mark-resolved")]
    public async Task<IActionResult> MarkResolved(
        long businessId,
        long notificationId,
        [FromBody] ResolveBusinessActivityNotificationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.Id == notificationId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (notification is null)
            return NotFound();

        var nowUtc = DateTime.UtcNow;

        notification.IsResolved = true;
        notification.ResolvedAtUtc = nowUtc;
        notification.ResolvedByUserId = request?.ResolvedByUserId;

        notification.IsSeen = true;
        notification.SeenAtUtc ??= nowUtc;

        notification.SnoozedUntilUtc = null;
        notification.SnoozedByUserId = null;

        notification.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("upsert")]
    public async Task<ActionResult<BusinessActivityNotificationDto>> Upsert(
        long businessId,
        [FromBody] UpsertBusinessActivityNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (businessId != request.BusinessId)
            return BadRequest("BusinessId iz rute i tela zahteva nije isti.");

        if (string.IsNullOrWhiteSpace(request.ActivityKey))
            return BadRequest("ActivityKey je obavezan.");

        if (string.IsNullOrWhiteSpace(request.Domain))
            return BadRequest("Domain je obavezan.");

        if (string.IsNullOrWhiteSpace(request.Kind))
            return BadRequest("Kind je obavezan.");

        var nowUtc = DateTime.UtcNow;
        var recipientType = string.IsNullOrWhiteSpace(request.RecipientType)
            ? BusinessActivityNotificationRecipients.Business
            : request.RecipientType.Trim();

        var recipientKey = NormalizeRecipientKey(request.RecipientKey);

        var notification = await _db.BusinessActivityNotifications
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.RecipientKey == recipientKey &&
                x.ActivityKey == request.ActivityKey,
                cancellationToken);

        if (notification is null)
        {
            notification = new BusinessActivityNotification
            {
                BusinessId = businessId,
                RecipientType = recipientType,
                RecipientKey = recipientKey,
                ActivityKey = request.ActivityKey.Trim(),
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc
            };

            _db.BusinessActivityNotifications.Add(notification);
        }

        notification.RecipientType = recipientType;
        notification.RecipientKey = recipientKey;
        notification.RecipientAppUserId = request.RecipientAppUserId;
        notification.RecipientCustomerProfileId = request.RecipientCustomerProfileId;
        notification.RecipientStaffMemberId = request.RecipientStaffMemberId;
        notification.RecipientOperationUnitId = request.RecipientOperationUnitId;

        notification.Domain = request.Domain.Trim();
        notification.Kind = request.Kind.Trim();
        notification.Title = NormalizeText(request.Title, "Obaveštenje");
        notification.MainText = NormalizeText(request.MainText, "-");
        notification.PreviewText = NormalizeText(request.PreviewText, "-");
        notification.Priority = request.Priority;
        notification.SortAtUtc = request.SortAtUtc == default ? nowUtc : request.SortAtUtc;

        notification.AppointmentId = request.AppointmentId;
        notification.ChangeRequestId = request.ChangeRequestId;
        notification.RestaurantOrderId = request.RestaurantOrderId;
        notification.RestaurantTableReservationId = request.RestaurantTableReservationId;
        notification.RestaurantAreaReservationId = request.RestaurantAreaReservationId;
        notification.ConversationId = request.ConversationId;
        notification.ChatMessageId = request.ChatMessageId;
        notification.SystemAlarmTriggerId = request.SystemAlarmTriggerId;
        notification.FitnessSessionId = request.FitnessSessionId;
        notification.FitnessSessionBookingId = request.FitnessSessionBookingId;
        notification.FitnessMemberId = request.FitnessMemberId;
        notification.FitnessMemberSessionDebtId = request.FitnessMemberSessionDebtId;
        notification.CustomerProfileId = request.CustomerProfileId;
        notification.BusinessCustomerId = request.BusinessCustomerId;
        notification.CustomerName = request.CustomerName;
        notification.CustomerPhone = request.CustomerPhone;
        notification.PayloadJson = request.PayloadJson;

        notification.IsResolved = false;
        notification.ResolvedAtUtc = null;
        notification.ResolvedByUserId = null;

        notification.UpdatedAtUtc = nowUtc;

        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(notification));
    }

    private static string NormalizeRecipientKey(string? recipientKey)
    {
        return string.IsNullOrWhiteSpace(recipientKey)
            ? "business"
            : recipientKey.Trim();
    }

    private static string NormalizeText(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static BusinessActivityNotificationDto ToDto(BusinessActivityNotification notification)
    {
        return new BusinessActivityNotificationDto
        {
            Id = notification.Id,
            BusinessId = notification.BusinessId,

            RecipientType = notification.RecipientType,
            RecipientKey = notification.RecipientKey,
            RecipientAppUserId = notification.RecipientAppUserId,
            RecipientCustomerProfileId = notification.RecipientCustomerProfileId,
            RecipientStaffMemberId = notification.RecipientStaffMemberId,
            RecipientOperationUnitId = notification.RecipientOperationUnitId,

            Domain = notification.Domain,
            Kind = notification.Kind,
            ActivityKey = notification.ActivityKey,
            Title = notification.Title,
            MainText = notification.MainText,
            PreviewText = notification.PreviewText,
            Priority = notification.Priority,

            IsSeen = notification.IsSeen,
            SeenAtUtc = notification.SeenAtUtc,
            IsResolved = notification.IsResolved,
            ResolvedAtUtc = notification.ResolvedAtUtc,
            SnoozedUntilUtc = notification.SnoozedUntilUtc,
            LastReminderAtUtc = notification.LastReminderAtUtc,

            SortAtUtc = notification.SortAtUtc,
            CreatedAtUtc = notification.CreatedAtUtc,

            AppointmentId = notification.AppointmentId,
            ChangeRequestId = notification.ChangeRequestId,
            RestaurantOrderId = notification.RestaurantOrderId,
            RestaurantTableReservationId = notification.RestaurantTableReservationId,
            RestaurantAreaReservationId = notification.RestaurantAreaReservationId,
            ConversationId = notification.ConversationId,
            ChatMessageId = notification.ChatMessageId,
            SystemAlarmTriggerId = notification.SystemAlarmTriggerId,
            FitnessSessionId = notification.FitnessSessionId,
            FitnessSessionBookingId = notification.FitnessSessionBookingId,
            FitnessMemberId = notification.FitnessMemberId,
            FitnessMemberSessionDebtId = notification.FitnessMemberSessionDebtId,
            CustomerProfileId = notification.CustomerProfileId,
            BusinessCustomerId = notification.BusinessCustomerId,
            CustomerName = notification.CustomerName,
            CustomerPhone = notification.CustomerPhone
        };
    }
}