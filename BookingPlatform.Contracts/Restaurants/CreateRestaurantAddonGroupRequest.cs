namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantAddonGroupRequest
{
    public long BusinessId { get; set; }

    public string Name { get; set; } = "";

    public int DisplayOrder { get; set; }
}