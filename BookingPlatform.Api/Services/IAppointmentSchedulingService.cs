using System;
using System.Threading;
using System.Threading.Tasks;

namespace BookingPlatform.Api.Services;

public interface IAppointmentSchedulingService
{
    Task<int> GetTotalServiceDurationAsync(
        long serviceId,
        int fallbackDurationMin,
        CancellationToken cancellationToken);

    Task<bool> IsStartAlignedToBusinessSlotGridAsync(
        long businessId,
        long staffMemberId,
        DateTime startAtUtc,
        CancellationToken cancellationToken);

    Task<AppointmentAvailabilityResult> CheckAvailabilityAsync(
        long businessId,
        long serviceId,
        long staffMemberId,
        long? resourceId,
        DateTime startAtUtc,
        DateTime endAtUtc,
        long? ignoreAppointmentId,
        bool ignoreWorkingHours,
        bool ignoreTimeOffBlocks,
        bool ignoreAppointmentConflicts,
        CancellationToken cancellationToken);

    Task CreateAppointmentStaffUsagesAsync(
        long appointmentId,
        long businessId,
        long serviceId,
        long primaryStaffMemberId,
        DateTime appointmentStartAtUtc,
        DateTime appointmentEndAtUtc,
        CancellationToken cancellationToken);
}

public sealed class AppointmentAvailabilityResult
{
    public bool IsAvailable { get; set; }
    public bool HasBusinessHoursViolation { get; set; }
    public bool HasStaffHoursViolation { get; set; }
    public bool HasTimeOffConflict { get; set; }
    public bool HasAppointmentConflict { get; set; }
    public bool HasResourceConflict { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
}