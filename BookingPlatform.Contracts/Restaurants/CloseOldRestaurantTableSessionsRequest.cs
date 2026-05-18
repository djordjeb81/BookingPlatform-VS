namespace BookingPlatform.Contracts.Restaurants;

public sealed class CloseOldRestaurantTableSessionsRequest
{
    public long BusinessId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public string? Note { get; set; }
}