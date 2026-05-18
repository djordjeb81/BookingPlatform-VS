using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOrderGuest : AuditableEntity
{
    public long OrderId { get; set; }

    public RestaurantOrder Order { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }

    public ICollection<RestaurantOrderItem> Items { get; set; } =
        new List<RestaurantOrderItem>();
}