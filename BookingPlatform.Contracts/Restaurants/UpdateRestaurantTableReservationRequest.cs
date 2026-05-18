namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantTableReservationRequest
{
    public long RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public int PartySize { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int? ExpectedDurationMin { get; set; }

    public string? Note { get; set; }

    public string? InternalNote { get; set; }
}