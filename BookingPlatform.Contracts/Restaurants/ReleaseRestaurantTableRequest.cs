namespace BookingPlatform.Contracts.Restaurants;

public sealed class ReleaseRestaurantTableRequest
{
    public long SessionId { get; set; }

    public string? Note { get; set; }
}