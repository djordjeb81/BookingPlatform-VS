using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Services;

public sealed class ServiceStepResourceRequirement : Entity
{
    public long ServiceStepId { get; set; }
    public long ResourceId { get; set; }
    public bool IsRequired { get; set; } = true;
    public int? SequenceOrder { get; set; }
    public int? UsageDurationMin { get; set; }
}