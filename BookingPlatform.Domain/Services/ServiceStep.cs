using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Services;

public sealed class ServiceStep : Entity
{
    public long ServiceId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public bool ClientPresenceRequired { get; set; } = true;
    public bool SameStaffAsPrevious { get; set; }
}