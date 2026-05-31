namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantOrderRequest
{
    public long BusinessId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public long? TableSessionId { get; set; }
    public int OrderType { get; set; } = 1;

    public int OrderSource { get; set; } = 1;

    public DateTime? RequestedPickupAtUtc { get; set; }

    public bool IsScheduledOrder { get; set; }

    public string? DeliveryAddress { get; set; }

    public long? DeliveryZoneId { get; set; }

    public string? DeliveryNote { get; set; }

    public double? DeliveryLatitude { get; set; }

    public double? DeliveryLongitude { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }
}