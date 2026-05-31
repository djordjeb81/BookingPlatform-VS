using BookingPlatform.Domain.SystemAlarms;
using BookingPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookingPlatform.Api.Services;

public sealed class SystemAlarmService : ISystemAlarmService
{
    private readonly BookingDbContext _db;
    private readonly IFirebasePushNotificationService _pushService;

    public SystemAlarmService(
        BookingDbContext db,
        IFirebasePushNotificationService pushService)
    {
        _db = db;
        _pushService = pushService;
    }

    public async Task<SystemAlarmTrigger> CreateRestaurantPreparationStartAlarmAsync(
        long businessId,
        long restaurantOrderId,
        DateTime triggerAtUtc,
        string orderNumberText,
        long? targetOperationUnitId,
        CancellationToken cancellationToken)
    {
        var safeTriggerAtUtc = EnsureUtc(triggerAtUtc);

        var existingAlarm = await _db.SystemAlarmTriggers
            .Where(x =>
                x.BusinessId == businessId &&
                x.RelatedOrderId == restaurantOrderId &&
                x.AlarmType == SystemAlarmType.RestaurantPreparationStart &&
                x.Status != SystemAlarmStatus.Cancelled &&
                x.Status != SystemAlarmStatus.Expired)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingAlarm is not null)
        {
            existingAlarm.TriggerAtUtc = safeTriggerAtUtc;
            existingAlarm.Status = SystemAlarmStatus.Pending;
            existingAlarm.FiredAtUtc = null;
            existingAlarm.StoppedAtUtc = null;
            existingAlarm.SnoozedUntilUtc = null;
            existingAlarm.CancelledAtUtc = null;
            existingAlarm.TargetOperationUnitId = targetOperationUnitId;
            existingAlarm.Title = "Vreme je za pripremu";
            existingAlarm.Message = BuildRestaurantPreparationMessage(orderNumberText);

            await _db.SaveChangesAsync(cancellationToken);

            await SendRestaurantAlarmPushAsync(
                existingAlarm,
                cancellationToken);

            return existingAlarm;
        }

        var alarm = new SystemAlarmTrigger
        {
            BusinessId = businessId,
            Domain = SystemAlarmDomain.Restaurant,
            AlarmType = SystemAlarmType.RestaurantPreparationStart,
            Status = SystemAlarmStatus.Pending,
            TargetType = targetOperationUnitId.HasValue
                ? SystemAlarmTargetType.OperationUnit
                : SystemAlarmTargetType.Business,
            TargetOperationUnitId = targetOperationUnitId,
            RelatedOrderId = restaurantOrderId,
            TriggerAtUtc = safeTriggerAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            Title = "Vreme je za pripremu",
            Message = BuildRestaurantPreparationMessage(orderNumberText),
            SoundKey = "restaurant_preparation_start",
            IsUrgent = true,
            RequiresUserAction = true,
            ActionKey = "open_restaurant_order"
        };

        _db.SystemAlarmTriggers.Add(alarm);
        await _db.SaveChangesAsync(cancellationToken);

        await SendRestaurantAlarmPushAsync(
            alarm,
            cancellationToken);

        return alarm;
    }

    public async Task<SystemAlarmTrigger> CreateRestaurantTableShouldBeFreeAlarmAsync(
    long businessId,
    long restaurantTableReservationId,
    long restaurantTableSessionId,
    long tableResourceId,
    string tableName,
    DateTime reservationAtUtc,
    int partySize,
    DateTime triggerAtUtc,
    long? targetOperationUnitId,
    CancellationToken cancellationToken)
    {
        var safeReservationAtUtc = EnsureUtc(reservationAtUtc);
        var safeTriggerAtUtc = EnsureUtc(triggerAtUtc);

        var actionKey = $"restaurant_table_should_be_free:{restaurantTableReservationId}";

        var existingAlarm = await _db.SystemAlarmTriggers
            .Where(x =>
                x.BusinessId == businessId &&
                x.AlarmType == SystemAlarmType.RestaurantTableShouldBeFree &&
                x.ActionKey == actionKey &&
                x.Status != SystemAlarmStatus.Cancelled &&
                x.Status != SystemAlarmStatus.Expired)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var safeTableName = string.IsNullOrWhiteSpace(tableName)
            ? $"Sto #{tableResourceId}"
            : tableName.Trim();

        var reservationLocal = safeReservationAtUtc.ToLocalTime();

        var title = "Sto uskoro treba osloboditi";
        var message =
            $"{safeTableName} treba osloboditi. " +
            $"Gosti dolaze u {reservationLocal:HH:mm}. " +
            $"Broj osoba: {partySize}.";

        var payload = JsonSerializer.Serialize(new
        {
            restaurantTableReservationId,
            restaurantTableSessionId,
            tableResourceId,
            tableName = safeTableName,
            reservationAtUtc = safeReservationAtUtc,
            partySize
        });

        if (existingAlarm is not null)
        {
            existingAlarm.TriggerAtUtc = safeTriggerAtUtc;
            existingAlarm.Status = SystemAlarmStatus.Pending;
            existingAlarm.FiredAtUtc = null;
            existingAlarm.StoppedAtUtc = null;
            existingAlarm.SnoozedUntilUtc = null;
            existingAlarm.CancelledAtUtc = null;
            existingAlarm.TargetType = targetOperationUnitId.HasValue
                ? SystemAlarmTargetType.OperationUnit
                : SystemAlarmTargetType.Business;
            existingAlarm.TargetOperationUnitId = targetOperationUnitId;
            existingAlarm.Title = title;
            existingAlarm.Message = message;
            existingAlarm.SoundKey = "restaurant_table_should_be_free";
            existingAlarm.IsUrgent = true;
            existingAlarm.RequiresUserAction = true;
            existingAlarm.PayloadJson = payload;

            await _db.SaveChangesAsync(cancellationToken);

            await SendRestaurantAlarmPushAsync(
                existingAlarm,
                cancellationToken);

            return existingAlarm;
        }

        var alarm = new SystemAlarmTrigger
        {
            BusinessId = businessId,
            Domain = SystemAlarmDomain.Restaurant,
            AlarmType = SystemAlarmType.RestaurantTableShouldBeFree,
            Status = SystemAlarmStatus.Pending,
            TargetType = targetOperationUnitId.HasValue
                ? SystemAlarmTargetType.OperationUnit
                : SystemAlarmTargetType.Business,
            TargetOperationUnitId = targetOperationUnitId,
            TriggerAtUtc = safeTriggerAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
            Title = title,
            Message = message,
            SoundKey = "restaurant_table_should_be_free",
            IsUrgent = true,
            RequiresUserAction = true,
            ActionKey = actionKey,
            PayloadJson = payload
        };

        _db.SystemAlarmTriggers.Add(alarm);
        await _db.SaveChangesAsync(cancellationToken);

        await SendRestaurantAlarmPushAsync(
            alarm,
            cancellationToken);

        return alarm;
    }

    public async Task<IReadOnlyList<SystemAlarmTrigger>> GetDueAlarmsAsync(
        long businessId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var safeNowUtc = EnsureUtc(nowUtc);

        return await _db.SystemAlarmTriggers
            .AsNoTracking()
.Where(x =>
    x.BusinessId == businessId &&
    (
        x.Status == SystemAlarmStatus.Pending ||
        x.Status == SystemAlarmStatus.Fired ||
        (
            x.Status == SystemAlarmStatus.Snoozed &&
            x.SnoozedUntilUtc != null &&
            x.SnoozedUntilUtc <= safeNowUtc
        )
    ) &&
    x.TriggerAtUtc <= safeNowUtc)
            .OrderBy(x => x.TriggerAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> MarkFiredAsync(
        long alarmId,
        CancellationToken cancellationToken)
    {
        var alarm = await _db.SystemAlarmTriggers
            .FirstOrDefaultAsync(x => x.Id == alarmId, cancellationToken);

        if (alarm is null)
        {
            return false;
        }

        if (alarm.Status == SystemAlarmStatus.Cancelled ||
            alarm.Status == SystemAlarmStatus.Stopped ||
            alarm.Status == SystemAlarmStatus.Expired)
        {
            return true;
        }

        if (alarm.Status != SystemAlarmStatus.Fired)
        {
            alarm.Status = SystemAlarmStatus.Fired;
            alarm.FiredAtUtc = DateTime.UtcNow;
        }

        alarm.SnoozedUntilUtc = null;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StopAsync(
        long alarmId,
        CancellationToken cancellationToken)
    {
        var alarm = await _db.SystemAlarmTriggers
            .FirstOrDefaultAsync(x => x.Id == alarmId, cancellationToken);

        if (alarm is null)
        {
            return false;
        }

        alarm.Status = SystemAlarmStatus.Stopped;
        alarm.StoppedAtUtc = DateTime.UtcNow;
        alarm.SnoozedUntilUtc = null;

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SnoozeAsync(
        long alarmId,
        int minutes,
        CancellationToken cancellationToken)
    {
        if (minutes is not (1 or 3 or 5 or 10 or 15))
        {
            throw new ArgumentOutOfRangeException(
                nameof(minutes),
                "Dozvoljeno odlaganje alarma je 1, 3, 5, 10 ili 15 minuta.");
        }

        var alarm = await _db.SystemAlarmTriggers
            .FirstOrDefaultAsync(x => x.Id == alarmId, cancellationToken);

        if (alarm is null)
        {
            return false;
        }

        alarm.Status = SystemAlarmStatus.Snoozed;
        alarm.SnoozedUntilUtc = DateTime.UtcNow.AddMinutes(minutes);

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelRelatedRestaurantOrderAlarmsAsync(
        long businessId,
        long restaurantOrderId,
        CancellationToken cancellationToken)
    {
        var alarms = await _db.SystemAlarmTriggers
            .Where(x =>
                x.BusinessId == businessId &&
                x.RelatedOrderId == restaurantOrderId &&
                x.Domain == SystemAlarmDomain.Restaurant &&
                x.Status != SystemAlarmStatus.Cancelled &&
                x.Status != SystemAlarmStatus.Stopped &&
                x.Status != SystemAlarmStatus.Expired)
            .ToListAsync(cancellationToken);

        if (alarms.Count == 0)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;

        foreach (var alarm in alarms)
        {
            alarm.Status = SystemAlarmStatus.Cancelled;
            alarm.CancelledAtUtc = nowUtc;
            alarm.SnoozedUntilUtc = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    long businessId,
    long restaurantTableReservationId,
    CancellationToken cancellationToken)
    {
        var actionKey = $"restaurant_table_should_be_free:{restaurantTableReservationId}";

        var alarms = await _db.SystemAlarmTriggers
            .Where(x =>
                x.BusinessId == businessId &&
                x.Domain == SystemAlarmDomain.Restaurant &&
                x.AlarmType == SystemAlarmType.RestaurantTableShouldBeFree &&
                x.ActionKey == actionKey &&
                x.Status != SystemAlarmStatus.Cancelled &&
                x.Status != SystemAlarmStatus.Stopped &&
                x.Status != SystemAlarmStatus.Expired)
            .ToListAsync(cancellationToken);

        if (alarms.Count == 0)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;

        foreach (var alarm in alarms)
        {
            alarm.Status = SystemAlarmStatus.Cancelled;
            alarm.CancelledAtUtc = nowUtc;
            alarm.SnoozedUntilUtc = null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task SendRestaurantAlarmPushAsync(
    SystemAlarmTrigger alarm,
    CancellationToken cancellationToken)
    {
        var data = new Dictionary<string, string>
        {
            ["type"] = "restaurant_alarm",
            ["businessId"] = alarm.BusinessId.ToString(),
            ["alarmId"] = alarm.Id.ToString(),
            ["alarmType"] = ((int)alarm.AlarmType).ToString(),
            ["domain"] = ((int)alarm.Domain).ToString(),
            ["title"] = alarm.Title,
            ["body"] = alarm.Message,
            ["soundKey"] = alarm.SoundKey,
            ["actionKey"] = alarm.ActionKey ?? string.Empty,
            ["payloadJson"] = alarm.PayloadJson ?? string.Empty
        };

        if (alarm.TargetOperationUnitId.HasValue)
        {
            data["targetOperationUnitId"] = alarm.TargetOperationUnitId.Value.ToString();
        }

        if (alarm.RelatedOrderId.HasValue)
        {
            data["relatedOrderId"] = alarm.RelatedOrderId.Value.ToString();
        }

        await _pushService.SendToBusinessUsersAsync(
            businessId: alarm.BusinessId,
            title: alarm.Title,
            body: alarm.Message,
            data: data,
            cancellationToken: cancellationToken);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
        {
            return value;
        }

        if (value.Kind == DateTimeKind.Unspecified)
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.ToUniversalTime();
    }

    private static string BuildRestaurantPreparationMessage(string orderNumberText)
    {
        var safeOrderNumberText = string.IsNullOrWhiteSpace(orderNumberText)
            ? "porudžbinu"
            : $"porudžbinu {orderNumberText.Trim()}";

        return $"Vreme je za početak pripreme za {safeOrderNumberText}.";
    }
}