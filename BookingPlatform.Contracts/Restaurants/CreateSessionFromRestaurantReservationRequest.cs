namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateSessionFromRestaurantReservationRequest
{
    public long? TableResourceId { get; set; }

    public string? Note { get; set; }
}