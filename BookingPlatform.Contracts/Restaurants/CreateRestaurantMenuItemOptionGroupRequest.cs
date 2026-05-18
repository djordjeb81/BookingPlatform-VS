namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantMenuItemOptionGroupRequest
{
    public long MenuItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int MinSelected { get; set; }

    public int MaxSelected { get; set; } = 1;

    public int DisplayOrder { get; set; }
}