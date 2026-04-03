namespace BookingPlatform.Contracts.Scheduling;

public sealed class BusinessWorkingHourDto
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}