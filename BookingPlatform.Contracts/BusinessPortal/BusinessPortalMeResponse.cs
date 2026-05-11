namespace BookingPlatform.Contracts.BusinessPortal;

public sealed class BusinessPortalMeResponse
{
    public long AppUserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool HasBusinessAccess { get; set; }

    public int BusinessCount { get; set; }
}