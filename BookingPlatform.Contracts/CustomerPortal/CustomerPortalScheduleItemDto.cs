namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerPortalScheduleItemDto
{
    public long Id { get; set; }

    public string ItemType { get; set; } = string.Empty;

    public long BusinessId { get; set; }

    public string? BusinessName { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Subtitle { get; set; }

    public DateTime? StartAtUtc { get; set; }

    public DateTime? EndAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string StatusText { get; set; } = string.Empty;

    public long? AppointmentId { get; set; }

    public long? RestaurantTableReservationId { get; set; }

    public string? DetailsText { get; set; }
}