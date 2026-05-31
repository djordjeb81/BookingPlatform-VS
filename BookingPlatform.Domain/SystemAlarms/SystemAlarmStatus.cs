namespace BookingPlatform.Domain.SystemAlarms;

public enum SystemAlarmStatus
{
    Pending = 0,
    Fired = 1,
    Stopped = 2,
    Snoozed = 3,
    Cancelled = 4,
    Expired = 5
}