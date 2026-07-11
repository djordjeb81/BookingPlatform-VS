using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class SharedRestaurantOrderItemOption : AuditableEntity
{
    public long SharedRestaurantOrderItemId { get; set; }

    public SharedRestaurantOrderItem SharedRestaurantOrderItem { get; set; } = null!;

    public long? RestaurantAddonId { get; set; }

    public string OptionNameSnapshot { get; set; } = string.Empty;

    public decimal PriceDeltaSnapshot { get; set; }

    public RestaurantAddonAmountMode AmountMode { get; set; } = RestaurantAddonAmountMode.Normal;
}
