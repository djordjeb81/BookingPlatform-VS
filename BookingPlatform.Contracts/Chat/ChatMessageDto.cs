namespace BookingPlatform.Contracts.Chat;

public sealed class ChatMessageDto
{
    public long Id { get; set; }

    public long ConversationId { get; set; }

    public string SenderType { get; set; } = "";

    public long? SenderUserId { get; set; }

    public string Text { get; set; } = "";

    public string? ActionType { get; set; }

    public long? AppointmentId { get; set; }

    public long? ChangeRequestId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadByBusinessAtUtc { get; set; }

    public DateTime? ReadByCustomerAtUtc { get; set; }
    public string? ChangeRequestStatus { get; set; }
}