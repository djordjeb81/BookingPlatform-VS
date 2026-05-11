using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Scheduling;

public sealed class StaffScheduleOverride : AuditableEntity
{
    public long StaffMemberId { get; set; }
    public DateTime Date { get; set; }
    public ScheduleOverrideType OverrideType { get; set; }
    public ScheduleSegmentType? ShiftType { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? EndTime { get; set; }
    public bool IsDayOff { get; set; }
    public string? Reason { get; set; }
}