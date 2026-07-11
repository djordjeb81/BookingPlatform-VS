namespace BookingPlatform.Contracts.BusinessActivityNotifications;

public sealed class SnoozeBusinessActivityNotificationRequest
{
    public DateTime SnoozedUntilUtc { get; set; }
}