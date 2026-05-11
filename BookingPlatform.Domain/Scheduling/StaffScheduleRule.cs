using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Scheduling;

public sealed class StaffScheduleRule : AuditableEntity
{
    public long StaffMemberId { get; set; }
    public int DayOfWeek { get; set; }
    public ScheduleWeekType WeekType { get; set; }
    public ScheduleSegmentType SegmentType { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsActive { get; set; } = true;
}