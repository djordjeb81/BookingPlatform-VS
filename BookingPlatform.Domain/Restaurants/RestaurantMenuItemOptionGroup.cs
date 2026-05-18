using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantMenuItemOptionGroup : AuditableEntity
{
    public long MenuItemId { get; set; }

    public RestaurantMenuItem MenuItem { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public bool IsRequired { get; set; }

    public int MinSelected { get; set; }

    public int MaxSelected { get; set; } = 1;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<RestaurantMenuItemOption> Options { get; set; } =
        new List<RestaurantMenuItemOption>();
}