using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Appointments;

public sealed class Appointment : AuditableEntity
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }
    public long? PrimaryStaffMemberId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.PendingApproval;

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public string? Notes { get; set; }
}