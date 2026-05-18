using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantMenuCategory : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RestaurantMenuItem> Items { get; set; } =
        new List<RestaurantMenuItem>();
}