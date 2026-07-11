namespace BookingPlatform.Domain.Customers;

public sealed class CustomerProfile
{
    public long Id { get; set; }

    public long? AppUserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Nickname { get; set; }

    public string? AvatarUrl { get; set; }

    public bool AllowUserSearch { get; set; }

    public bool AllowChatDiscovery { get; set; }

    public string? DefaultDeliveryAddress { get; set; }

    public string? DefaultDeliveryCity { get; set; }

    public string? DefaultDeliveryStreet { get; set; }

    public string? DefaultDeliveryStreetNumber { get; set; }

    public string? DefaultDeliveryApartment { get; set; }

    public string? DefaultDeliveryNote { get; set; }

    public double? DefaultDeliveryLatitude { get; set; }

    public double? DefaultDeliveryLongitude { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
