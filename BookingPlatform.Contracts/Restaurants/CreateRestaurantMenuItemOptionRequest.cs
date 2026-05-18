namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantMenuItemOptionRequest
{
    public long OptionGroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int DisplayOrder { get; set; }
}