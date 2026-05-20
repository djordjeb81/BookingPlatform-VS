using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantAddon : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long AddonGroupId { get; set; }

    public RestaurantAddonGroup AddonGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsAvailable { get; set; } = true;
}