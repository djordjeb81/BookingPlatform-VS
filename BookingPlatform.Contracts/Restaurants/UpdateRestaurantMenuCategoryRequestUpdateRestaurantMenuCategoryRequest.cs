namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantMenuCategoryRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }
}