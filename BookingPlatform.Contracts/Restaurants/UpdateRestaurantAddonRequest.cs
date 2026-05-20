namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantAddonRequest
{
    public string Name { get; set; } = "";

    public decimal PriceDelta { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAvailable { get; set; } = true;
}