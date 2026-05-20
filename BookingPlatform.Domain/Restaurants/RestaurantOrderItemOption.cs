using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderItemOption : AuditableEntity
{
    public long OrderItemId { get; set; }

    public RestaurantOrderItem OrderItem { get; set; } = null!;

    public long? MenuItemOptionId { get; set; }

    public RestaurantMenuItemOption? MenuItemOption { get; set; }

    public long? RestaurantAddonId { get; set; }

    public RestaurantAddon? RestaurantAddon { get; set; }

    public string OptionNameSnapshot { get; set; } = string.Empty;

    public decimal PriceDeltaSnapshot { get; set; }

    // 0 = normalno, 1 = malo, 2 = više
    public RestaurantAddonAmountMode AmountMode { get; set; } = RestaurantAddonAmountMode.Normal;
}