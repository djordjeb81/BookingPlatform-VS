namespace BookingPlatform.Contracts.BusinessActivityNotifications;

public sealed class MarkBusinessActivityNotificationSeenRequest
{
    public long? SeenByUserId { get; set; }
}