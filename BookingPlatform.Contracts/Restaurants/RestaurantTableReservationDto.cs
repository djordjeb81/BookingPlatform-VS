namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantTableReservationDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public string? TableName { get; set; }

    public int PartySize { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int? ExpectedDurationMin { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string? InternalNote { get; set; }

    public DateTime? RespondedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? ArrivedAtUtc { get; set; }

    public long? CreatedTableSessionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}