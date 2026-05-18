namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderItemDto
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long? OrderGuestId { get; set; }

    public string? OrderGuestName { get; set; }

    public long MenuItemId { get; set; }

    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    public decimal UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public decimal LineSubtotal { get; set; }

    public bool SendToKitchenSnapshot { get; set; }

    public string? Note { get; set; }

    public List<RestaurantOrderItemOptionDto> Options { get; set; } = new();
}