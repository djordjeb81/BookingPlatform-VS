namespace BookingPlatform.Contracts.Chat;

public sealed class SendBusinessBroadcastMessageRequest
{
    public string Title { get; set; } = "";

    public string Text { get; set; } = "";

    public DateTime? ValidFromUtc { get; set; }

    public DateTime? ValidToUtc { get; set; }

    public bool OnlyActiveCustomers { get; set; } = true;
}