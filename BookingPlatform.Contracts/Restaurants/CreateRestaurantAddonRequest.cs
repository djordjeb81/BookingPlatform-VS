namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantAddonRequest
{
    public string Name { get; set; } = "";

    public decimal PriceDelta { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsAvailable { get; set; } = true;
}