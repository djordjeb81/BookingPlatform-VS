using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantMenuItem : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long CategoryId { get; set; }

    public RestaurantMenuCategory Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "RSD";

    public bool IsAvailable { get; set; } = true;

    public bool SendToKitchen { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public ICollection<RestaurantMenuItemOptionGroup> OptionGroups { get; set; } =
    new List<RestaurantMenuItemOptionGroup>();
}