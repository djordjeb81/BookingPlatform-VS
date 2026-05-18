namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantOrderItemRequest
{
    public long? OrderGuestId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Note { get; set; }

    public List<long> MenuItemOptionIds { get; set; } = new();
}