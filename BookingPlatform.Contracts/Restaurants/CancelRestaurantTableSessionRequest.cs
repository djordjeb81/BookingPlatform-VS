namespace BookingPlatform.Contracts.Restaurants;

public sealed class CancelRestaurantTableSessionRequest
{
    public long SessionId { get; set; }

    public string? Note { get; set; }
}