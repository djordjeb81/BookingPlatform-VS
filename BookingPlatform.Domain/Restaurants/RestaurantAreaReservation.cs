using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantAreaReservation : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long RestaurantAreaId { get; set; }

    public RestaurantArea RestaurantArea { get; set; } = null!;

    public int PartySize { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int? ExpectedDurationMin { get; set; }

    public RestaurantAreaReservationStatus Status { get; set; } =
        RestaurantAreaReservationStatus.PendingApproval;

    public string? Note { get; set; }

    public string? InternalNote { get; set; }

    public DateTime? RespondedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? ArrivedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
}