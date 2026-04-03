namespace BookingPlatform.Contracts.Scheduling;

public sealed class StaffWorkingHourDto
{
    public long Id { get; set; }
    public long StaffMemberId { get; set; }
    public int DayOfWeek { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}