using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Chat;

public sealed class ChatMessage : AuditableEntity
{
    public long ConversationId { get; set; }

    public ChatSenderType SenderType { get; set; }

    public long? SenderUserId { get; set; }

    public string Text { get; set; } = string.Empty;

    public string? ActionType { get; set; }

    public long? AppointmentId { get; set; }

    public long? ChangeRequestId { get; set; }

    public DateTime? ReadByBusinessAtUtc { get; set; }

    public DateTime? ReadByCustomerAtUtc { get; set; }
}