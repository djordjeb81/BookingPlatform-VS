namespace BookingPlatform.Contracts.Customers;

public sealed class BusinessCustomerEmailLookupResponse
{
    public string Email { get; set; } = string.Empty;

    public bool CustomerProfileExists { get; set; }

    public long? CustomerProfileId { get; set; }

    public bool BusinessCustomerExists { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? AppUserId { get; set; }

    public string? FullName { get; set; }

    public string? Phone { get; set; }
}