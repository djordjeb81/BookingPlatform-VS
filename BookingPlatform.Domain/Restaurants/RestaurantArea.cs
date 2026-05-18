using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantArea : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int CanvasWidth { get; set; } = 1000;

    public int CanvasHeight { get; set; } = 1000;

    public string? BoundaryPointsJson { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsReservableAsWhole { get; set; }

    public long? WholeAreaResourceId { get; set; }
}