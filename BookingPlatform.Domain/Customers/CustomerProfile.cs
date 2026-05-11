namespace BookingPlatform.Domain.Customers;

public sealed class CustomerProfile
{
    public long Id { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}