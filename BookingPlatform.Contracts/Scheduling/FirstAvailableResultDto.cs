namespace BookingPlatform.Contracts.Scheduling;

public sealed class FirstAvailableResultDto
{
    public long StaffMemberId { get; set; }
    public string? StaffDisplayName { get; set; }

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
    public string DateLabel { get; set; } = string.Empty;
}