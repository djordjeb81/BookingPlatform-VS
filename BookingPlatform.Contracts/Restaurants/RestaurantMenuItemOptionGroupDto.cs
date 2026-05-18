namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantMenuItemOptionGroupDto
{
    public long Id { get; set; }

    public long MenuItemId { get; set; }

    public string MenuItemName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int MinSelected { get; set; }

    public int MaxSelected { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public List<RestaurantMenuItemOptionDto> Options { get; set; } = new();
}