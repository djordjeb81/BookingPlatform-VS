namespace BookingPlatform.Contracts.Resources;

public sealed class ServiceResourceUsageDto
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }

    // Legacy: jedan radnik. Nova logika koristi StaffMemberIds.
    public long? StaffId { get; set; }

    public List<long> StaffMemberIds { get; set; } = new();

    public string ResourceName { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public string? CustomerDisplayText { get; set; }
    public int StartMinute { get; set; }
    public int DurationMin { get; set; }
    public bool IsRequired { get; set; }
}