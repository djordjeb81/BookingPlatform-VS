namespace BookingPlatform.Contracts.Restaurants;

public sealed class SetRestaurantOrderStatusRequest
{
    public int Status { get; set; }

    public string? Note { get; set; }
}