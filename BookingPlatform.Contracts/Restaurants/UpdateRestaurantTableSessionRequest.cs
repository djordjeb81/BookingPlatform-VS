namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantTableSessionRequest
{
    public int? PartySize { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }
}