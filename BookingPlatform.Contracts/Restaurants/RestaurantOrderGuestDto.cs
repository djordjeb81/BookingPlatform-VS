namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderGuestDto
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }

    public List<RestaurantOrderItemDto> Items { get; set; } = new();
}