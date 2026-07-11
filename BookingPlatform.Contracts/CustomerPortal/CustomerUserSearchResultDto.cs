namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerUserSearchResultDto
{
    public long CustomerProfileId { get; set; }

    public long? AppUserId { get; set; }

    public string DisplayName { get; set; } = "Klijent";

    public string? FullName { get; set; }

    public string? Nickname { get; set; }

    public string? PhoneMasked { get; set; }

    public string? EmailMasked { get; set; }

    public string? AvatarUrl { get; set; }
}
