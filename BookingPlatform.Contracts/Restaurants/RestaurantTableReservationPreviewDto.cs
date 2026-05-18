namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantTableReservationPreviewDto
{
    public long ReservationId { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int PartySize { get; set; }

    public string CustomerName { get; set; } = "";

    public string? CustomerPhone { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = "";

    public int? ExpectedDurationMin { get; set; }
}