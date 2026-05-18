namespace BookingPlatform.Contracts.Restaurants;

public sealed class MoveRestaurantTableSessionRequest
{
    public long SessionId { get; set; }

    public long NewTableResourceId { get; set; }

    public string? Note { get; set; }
}