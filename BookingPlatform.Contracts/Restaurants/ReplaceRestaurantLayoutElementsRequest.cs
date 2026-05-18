namespace BookingPlatform.Contracts.Restaurants;

public sealed class ReplaceRestaurantLayoutElementsRequest
{
    public List<RestaurantLayoutElementSaveItemDto> Elements { get; set; } = new();
}

public sealed class RestaurantLayoutElementSaveItemDto
{
    public int ElementType { get; set; }

    public string? Label { get; set; }

    public decimal X { get; set; }

    public decimal Y { get; set; }

    public decimal Width { get; set; }

    public decimal Height { get; set; }

    public int RotationDeg { get; set; }

    public int ShapeType { get; set; }

    public string? PointsJson { get; set; }

    public bool IsObstacle { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}