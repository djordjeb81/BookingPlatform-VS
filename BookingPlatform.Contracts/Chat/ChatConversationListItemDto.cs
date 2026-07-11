namespace BookingPlatform.Contracts.Chat;

public sealed class ChatConversationListItemDto
{
    public long Id { get; set; }

    public string ConversationTargetType { get; set; } = "Business";

    public long BusinessId { get; set; }

    public long? BusinessCustomerId { get; set; }
    public long? CustomerProfileId { get; set; }
    public long? AppUserId { get; set; }

    public string CustomerName { get; set; } = "";
    public string? CustomerDisplayName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastMessageText { get; set; }

    public int UnreadForBusinessCount { get; set; }
    public int UnreadForCustomerCount { get; set; }
}
