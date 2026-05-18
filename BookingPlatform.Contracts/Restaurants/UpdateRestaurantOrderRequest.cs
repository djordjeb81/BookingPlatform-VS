namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantOrderRequest
{
    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }
    public DateTime? RequestedPickupAtUtc { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? DeliveryNote { get; set; }
}