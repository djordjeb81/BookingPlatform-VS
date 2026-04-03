using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Scheduling;

public sealed class TimeOffBlock : AuditableEntity
{
    public long BusinessId { get; set; }
    public long? StaffMemberId { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public TimeOffBlockType BlockType { get; set; }

    public string? Reason { get; set; }
}