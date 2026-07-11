namespace BookingPlatform.Contracts.Chat;

public sealed class ChatSearchResultDto
{
    public string TargetType { get; set; } = "";

    public long TargetId { get; set; }

    public string DisplayName { get; set; } = "";

    public string? Subtitle { get; set; }

    public long? BusinessId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? AppUserId { get; set; }

    public string? PhoneMasked { get; set; }

    public string? EmailMasked { get; set; }

    public bool CanChat { get; set; } = true;
}
