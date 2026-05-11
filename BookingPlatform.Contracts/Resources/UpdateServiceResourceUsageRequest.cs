namespace BookingPlatform.Contracts.Resources;

public sealed class UpdateServiceResourceUsageRequest
{
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }

    // Legacy: jedan radnik. Nova logika koristi StaffMemberIds.
    public long? StaffId { get; set; }
    public string? CustomerDisplayText { get; set; }
    public List<long> StaffMemberIds { get; set; } = new();

    public int StartMinute { get; set; }
    public int DurationMin { get; set; }
    public bool IsRequired { get; set; }
}