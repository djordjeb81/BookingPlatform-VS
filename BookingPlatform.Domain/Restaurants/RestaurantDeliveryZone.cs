using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantDeliveryZone : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal DeliveryFeeAmount { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}