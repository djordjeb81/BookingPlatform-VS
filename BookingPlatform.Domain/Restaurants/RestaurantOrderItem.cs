using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderItem : AuditableEntity
{
    public long OrderId { get; set; }

    public RestaurantOrder Order { get; set; } = null!;

    public long? OrderGuestId { get; set; }

    public RestaurantOrderGuest? OrderGuest { get; set; }

    public long MenuItemId { get; set; }

    public RestaurantMenuItem MenuItem { get; set; } = null!;

    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    public decimal UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public decimal LineSubtotal { get; set; }

    public bool SendToKitchenSnapshot { get; set; } = true;

    public bool IsReady { get; set; }

    public DateTime? ReadyAtUtc { get; set; }

    public string? Note { get; set; }

    public ICollection<RestaurantOrderItemOption> Options { get; set; } =
        new List<RestaurantOrderItemOption>();
}