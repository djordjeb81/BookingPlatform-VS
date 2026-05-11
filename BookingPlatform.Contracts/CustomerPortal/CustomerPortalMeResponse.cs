namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerPortalMeResponse
{
    public long AppUserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool HasCustomerProfile { get; set; }

    public long? CustomerProfileId { get; set; }

    public string? CustomerName { get; set; }

    public string? Phone { get; set; }
}