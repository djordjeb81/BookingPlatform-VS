namespace BookingPlatform.Contracts.Chat;

public sealed class SendBusinessBroadcastMessageResponse
{
    public long BusinessId { get; set; }

    public int TargetCount { get; set; }

    public int SentCount { get; set; }

    public int SkippedCount { get; set; }

    public string Message { get; set; } = "";
}