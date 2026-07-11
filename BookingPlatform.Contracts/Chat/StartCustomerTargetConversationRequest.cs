namespace BookingPlatform.Contracts.Chat;

public sealed class StartCustomerTargetConversationRequest
{
    public string TargetType { get; set; } = "";

    public long TargetId { get; set; }
}
