using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Scheduling;

public sealed class BusinessWorkingHour : Entity
{
    public long BusinessId { get; set; }
    public int DayOfWeek { get; set; }   // 1 = Monday ... 7 = Sunday
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public bool IsClosed { get; set; }
}