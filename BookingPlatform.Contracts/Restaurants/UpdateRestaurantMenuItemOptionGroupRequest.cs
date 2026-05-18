namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantMenuItemOptionGroupRequest
{
    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int MinSelected { get; set; }

    public int MaxSelected { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}