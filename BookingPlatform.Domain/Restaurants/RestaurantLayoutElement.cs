using BookingPlatform.Domain.Common;
using BookingPlatform.Domain.Resources;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantLayoutElement : AuditableEntity
{
    public long RestaurantAreaId { get; set; }

    public RestaurantArea RestaurantArea { get; set; } = null!;

    public RestaurantLayoutElementType ElementType { get; set; }

    public string? Label { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public int RotationDeg { get; set; }

    public LayoutShapeType ShapeType { get; set; } = LayoutShapeType.Rectangle;

    public string? PointsJson { get; set; }

    public bool IsObstacle { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}