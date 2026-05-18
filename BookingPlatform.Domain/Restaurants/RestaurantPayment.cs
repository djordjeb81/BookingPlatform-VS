using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantPayment : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long TableSessionId { get; set; }

    public RestaurantTableSession TableSession { get; set; } = null!;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public RestaurantPaymentMethod Method { get; set; } = RestaurantPaymentMethod.Cash;

    public RestaurantPaymentStatus Status { get; set; } = RestaurantPaymentStatus.Paid;

    public string? Note { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
}