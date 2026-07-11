namespace BookingPlatform.Contracts.Chat;

public sealed class ChatConversationMemberDto
{
    public long Id { get; set; }

    public long ConversationId { get; set; }

    public long CustomerProfileId { get; set; }

    public long? AppUserId { get; set; }

    public string DisplayName { get; set; } = "Klijent";

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
