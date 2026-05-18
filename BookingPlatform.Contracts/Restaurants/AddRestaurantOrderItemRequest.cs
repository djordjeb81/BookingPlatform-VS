namespace BookingPlatform.Contracts.Restaurants;

public sealed class AddRestaurantOrderItemRequest
{
    public long? OrderGuestId { get; set; }

    public long MenuItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Note { get; set; }

    public List<long> MenuItemOptionIds { get; set; } = new();
}