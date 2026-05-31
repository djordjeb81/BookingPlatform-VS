using BookingPlatform.Domain.SystemAlarms;

namespace BookingPlatform.Api.Services;

public interface ISystemAlarmService
{
    Task<SystemAlarmTrigger> CreateRestaurantPreparationStartAlarmAsync(
        long businessId,
        long restaurantOrderId,
        DateTime triggerAtUtc,
        string orderNumberText,
        long? targetOperationUnitId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SystemAlarmTrigger>> GetDueAlarmsAsync(
        long businessId,
        DateTime nowUtc,
        CancellationToken cancellationToken);

    Task<bool> MarkFiredAsync(
        long alarmId,
        CancellationToken cancellationToken);

    Task<bool> StopAsync(
        long alarmId,
        CancellationToken cancellationToken);

    Task<bool> SnoozeAsync(
        long alarmId,
        int minutes,
        CancellationToken cancellationToken);

    Task<bool> CancelRelatedRestaurantOrderAlarmsAsync(
        long businessId,
        long restaurantOrderId,
        CancellationToken cancellationToken);

    Task<SystemAlarmTrigger> CreateRestaurantTableShouldBeFreeAlarmAsync(
    long businessId,
    long restaurantTableReservationId,
    long restaurantTableSessionId,
    long tableResourceId,
    string tableName,
    DateTime reservationAtUtc,
    int partySize,
    DateTime triggerAtUtc,
    long? targetOperationUnitId,
    CancellationToken cancellationToken);

    Task<bool> CancelRestaurantTableShouldBeFreeAlarmForReservationAsync(
    long businessId,
    long restaurantTableReservationId,
    CancellationToken cancellationToken);
}