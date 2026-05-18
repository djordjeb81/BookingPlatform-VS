using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantMenuItemOption : AuditableEntity
{
    public long OptionGroupId { get; set; }

    public RestaurantMenuItemOptionGroup OptionGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}