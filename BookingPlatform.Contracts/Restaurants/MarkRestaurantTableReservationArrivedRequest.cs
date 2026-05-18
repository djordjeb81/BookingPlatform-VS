namespace BookingPlatform.Contracts.Restaurants;

public sealed class MarkRestaurantTableReservationArrivedRequest
{
    public long? TableResourceId { get; set; }

    public string? InternalNote { get; set; }
}