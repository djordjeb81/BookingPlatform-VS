using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Chat;

public sealed class ChatConversation : AuditableEntity
{
    public long BusinessId { get; set; }

    public long? BusinessCustomerId { get; set; }
    public long? CustomerProfileId { get; set; }
    public long? AppUserId { get; set; }

    public DateTime? LastMessageAtUtc { get; set; }
    public string? LastMessageText { get; set; }

    public int UnreadForBusinessCount { get; set; }
    public int UnreadForCustomerCount { get; set; }

    public bool IsActive { get; set; } = true;
}