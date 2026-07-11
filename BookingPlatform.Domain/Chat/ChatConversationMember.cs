using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Chat;

public sealed class ChatConversationMember : AuditableEntity
{
    public long ConversationId { get; set; }

    public long CustomerProfileId { get; set; }

    public long? AppUserId { get; set; }

    public string DisplayNameSnapshot { get; set; } = string.Empty;

    public long CreatedByAppUserId { get; set; }

    public bool IsActive { get; set; } = true;
}
