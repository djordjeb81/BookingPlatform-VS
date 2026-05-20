namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantAddonGroupRequest
{
    public string Name { get; set; } = "";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}