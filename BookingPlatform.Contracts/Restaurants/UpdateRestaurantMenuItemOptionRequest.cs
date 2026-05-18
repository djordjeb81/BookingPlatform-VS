namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantMenuItemOptionRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public bool IsAvailable { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}