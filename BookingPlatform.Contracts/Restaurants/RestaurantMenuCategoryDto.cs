namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantMenuCategoryDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public List<RestaurantMenuItemDto> Items { get; set; } = new();
}