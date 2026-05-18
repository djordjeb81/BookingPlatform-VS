namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantMenuCategoryRequest
{
    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }
}