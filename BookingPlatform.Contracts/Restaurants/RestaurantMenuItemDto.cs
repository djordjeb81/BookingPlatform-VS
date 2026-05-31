namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantMenuItemDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "RSD";

    public bool IsAvailable { get; set; }

    public bool SendToKitchen { get; set; }

    public int PreparationTimeMin { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
    public List<RestaurantMenuItemOptionGroupDto> OptionGroups { get; set; } = new();

}