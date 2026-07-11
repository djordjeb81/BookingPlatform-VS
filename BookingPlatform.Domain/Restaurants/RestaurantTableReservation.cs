using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Resources;
using BookingPlatform.Domain.Auth;
using BookingPlatform.Domain.Customers;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantTableReservation : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long RestaurantAreaId { get; set; }

    public RestaurantArea RestaurantArea { get; set; } = null!;

    // Može biti null ako klijent rezerviše samo broj osoba,
    // a biznis kasnije dodeli konkretan sto.
    public long? TableResourceId { get; set; }

    public Resource? TableResource { get; set; }

    public long? CustomerProfileId { get; set; }

    public CustomerProfile? CustomerProfile { get; set; }

    public long? AppUserId { get; set; }

    public AppUser? AppUser { get; set; }

    public long? BusinessCustomerId { get; set; }

    public BusinessCustomer? BusinessCustomer { get; set; }

    public int PartySize { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public DateTime ReservationAtUtc { get; set; }

    public int? ExpectedDurationMin { get; set; }

    public RestaurantTableReservationStatus Status { get; set; } =
        RestaurantTableReservationStatus.PendingApproval;

    public string? Note { get; set; }

    public string? InternalNote { get; set; }

    public DateTime? RespondedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? ArrivedAtUtc { get; set; }

    public long? CreatedTableSessionId { get; set; }

    public RestaurantTableSession? CreatedTableSession { get; set; }
}