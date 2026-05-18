namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderItemOptionDto
{
    public long Id { get; set; }

    public long OrderItemId { get; set; }

    public long MenuItemOptionId { get; set; }

    public string OptionNameSnapshot { get; set; } = string.Empty;

    public decimal PriceDeltaSnapshot { get; set; }
}