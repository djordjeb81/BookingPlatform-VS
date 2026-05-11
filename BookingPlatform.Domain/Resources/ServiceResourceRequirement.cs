using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Resources;

public sealed class ServiceResourceRequirement : AuditableEntity
{
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }
    public bool IsRequired { get; set; } = true;
}