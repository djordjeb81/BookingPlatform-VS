namespace BookingPlatform.Contracts.Restaurants;

public sealed class CancelRestaurantAreaReservationRequest
{
    public string? Note { get; set; }

    public string? InternalNote { get; set; }
}