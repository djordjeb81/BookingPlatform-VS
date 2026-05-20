namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderItemOptionDto
{
    public long Id { get; set; }

    public long OrderItemId { get; set; }

    public long? MenuItemOptionId { get; set; }

    public long? RestaurantAddonId { get; set; }

    public string OptionNameSnapshot { get; set; } = string.Empty;

    public decimal PriceDeltaSnapshot { get; set; }

    // 0 = normalno, 1 = malo, 2 = više
    public int AmountMode { get; set; }

    public string AmountModeText { get; set; } = "";
}