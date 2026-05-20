namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantAddonGroupDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = "";

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public List<RestaurantAddonDto> Addons { get; set; } = new();
}