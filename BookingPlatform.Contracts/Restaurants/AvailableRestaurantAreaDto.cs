namespace BookingPlatform.Contracts.Restaurants;

public sealed class AvailableRestaurantAreaDto
{
    public long RestaurantAreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public bool IsBestFit { get; set; }

    public int CapacityDifference { get; set; }

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public string? BoundaryPointsJson { get; set; }
}