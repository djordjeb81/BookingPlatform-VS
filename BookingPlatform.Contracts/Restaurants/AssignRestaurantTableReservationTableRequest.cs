namespace BookingPlatform.Contracts.Restaurants;

public sealed class AssignRestaurantTableReservationTableRequest
{
    public long TableResourceId { get; set; }

    public string? InternalNote { get; set; }
}