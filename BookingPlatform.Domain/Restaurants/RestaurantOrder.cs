using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrder : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public DateOnly OrderDateLocal { get; set; }

    public int DailyOrderNumber { get; set; }

    public long? RestaurantAreaId { get; set; }

    public RestaurantArea? RestaurantArea { get; set; }

    public long? TableResourceId { get; set; }

    public long? TableSessionId { get; set; }
    public RestaurantOrderType OrderType { get; set; } = RestaurantOrderType.DineIn;

    public RestaurantOrderSource OrderSource { get; set; } = RestaurantOrderSource.RestaurantDesk;

    public DateTime? RequestedPickupAtUtc { get; set; }

    public bool IsScheduledOrder { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? DeliveryNote { get; set; }

    public double? DeliveryLatitude { get; set; }

    public double? DeliveryLongitude { get; set; }

    public long? DeliveryZoneId { get; set; }

    public string? DeliveryZoneNameSnapshot { get; set; }

    public decimal DeliveryFeeAmount { get; set; }

    public decimal DeliveryMinimumOrderAmountSnapshot { get; set; }

    public RestaurantTableSession? TableSession { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }

    public RestaurantOrderStatus Status { get; set; } = RestaurantOrderStatus.Draft;

    public RestaurantKitchenDecisionStatus KitchenDecisionStatus { get; set; } =
    RestaurantKitchenDecisionStatus.None;

    public DateTime? KitchenAcceptedAtUtc { get; set; }

    public int? KitchenAcceptLaterMinutes { get; set; }

    public DateTime? KitchenRejectedAtUtc { get; set; }

    public string? KitchenRejectReason { get; set; }

    public string? KitchenRejectNote { get; set; }

    public decimal SubtotalAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public ICollection<RestaurantOrderGuest> Guests { get; set; } =
        new List<RestaurantOrderGuest>();

    public ICollection<RestaurantOrderItem> Items { get; set; } =
        new List<RestaurantOrderItem>();

    public ICollection<RestaurantOrderMessage> Messages { get; set; } =
    new List<RestaurantOrderMessage>();
}