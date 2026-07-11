namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CreateCustomerRestaurantTableReservationRequest
{
    public long RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public int PartySize { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int? ExpectedDurationMin { get; set; }

    public string? Note { get; set; }
}