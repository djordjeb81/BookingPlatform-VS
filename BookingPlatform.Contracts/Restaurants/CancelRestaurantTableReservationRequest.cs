namespace BookingPlatform.Contracts.Restaurants;

public sealed class CancelRestaurantTableReservationRequest
{
    public string? Note { get; set; }

    public string? InternalNote { get; set; }
}