namespace BookingPlatform.Contracts.Scheduling;

public sealed class StaffScheduleRuleDto
{
    public long Id { get; set; }
    public long StaffMemberId { get; set; }
    public int DayOfWeek { get; set; }
    public int WeekType { get; set; }
    public int SegmentType { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}