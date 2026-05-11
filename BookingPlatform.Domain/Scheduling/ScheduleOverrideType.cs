namespace BookingPlatform.Domain.Scheduling;

public enum ScheduleOverrideType
{
    ReplaceWithFixed = 0,
    ReplaceWithShift1 = 1,
    ReplaceWithShift2 = 2,
    ReplaceWithSplitShift = 3,
    DayOff = 4
}