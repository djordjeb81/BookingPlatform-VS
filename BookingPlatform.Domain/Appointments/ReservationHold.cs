using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Appointments;

public sealed class ReservationHold : AuditableEntity
{
    public long BusinessId { get; set; }
    public long ServiceId { get; set; }

    public string HoldToken { get; set; } = string.Empty;

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    public bool IsConverted { get; set; }
    public bool IsExpired { get; set; }
}