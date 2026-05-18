using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderItemOption : AuditableEntity
{
    public long OrderItemId { get; set; }

    public RestaurantOrderItem OrderItem { get; set; } = null!;

    public long MenuItemOptionId { get; set; }

    public RestaurantMenuItemOption MenuItemOption { get; set; } = null!;

    public string OptionNameSnapshot { get; set; } = string.Empty;

    public decimal PriceDeltaSnapshot { get; set; }
}