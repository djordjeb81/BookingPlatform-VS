namespace BookingPlatform.Contracts.Restaurants;

public sealed class RejectRestaurantOrderByKitchenRequest
{
    public string Reason { get; set; } = "";

    public string? Note { get; set; }
}