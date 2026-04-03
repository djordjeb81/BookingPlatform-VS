using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Appointments;

public sealed class AppointmentAuditLog : AuditableEntity
{
    public long AppointmentId { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public string? Message { get; set; }

    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
}