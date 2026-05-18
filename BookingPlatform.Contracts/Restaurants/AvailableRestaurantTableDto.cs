namespace BookingPlatform.Contracts.Restaurants;

public sealed class AvailableRestaurantTableDto
{
    public long TableResourceId { get; set; }

    public string TableName { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public bool IsBestFit { get; set; }

    public int CapacityDifference { get; set; }

    public decimal? LayoutX { get; set; }

    public decimal? LayoutY { get; set; }

    public decimal? LayoutWidth { get; set; }

    public decimal? LayoutHeight { get; set; }

    public int LayoutRotationDeg { get; set; }

    public int LayoutShape { get; set; }

    public string? LayoutPointsJson { get; set; }
}