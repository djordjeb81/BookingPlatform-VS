namespace BookingPlatform.Contracts.Scheduling;

public sealed class SetBusinessWorkingHourRequest
{
    public long BusinessId { get; set; }
    public int DayOfWeek { get; set; }   // 1 = Monday ... 7 = Sunday
    public string StartTime { get; set; } = string.Empty; // "09:00"
    public string EndTime { get; set; } = string.Empty;   // "17:00"
    public bool IsClosed { get; set; }
}