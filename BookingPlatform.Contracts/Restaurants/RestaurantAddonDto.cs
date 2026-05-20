namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantAddonDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long AddonGroupId { get; set; }

    public string Name { get; set; } = "";

    public decimal PriceDelta { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsAvailable { get; set; }
}