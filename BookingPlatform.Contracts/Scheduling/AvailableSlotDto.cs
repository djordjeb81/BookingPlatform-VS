namespace BookingPlatform.Contracts.Scheduling;

public sealed class AvailableSlotDto
{
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string StartLabel { get; set; } = string.Empty;
    public string EndLabel { get; set; } = string.Empty;
}