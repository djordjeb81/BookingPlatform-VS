namespace BookingPlatform.Contracts.Resources;

public sealed class UpdateResourceRequest
{
    public string Name { get; set; } = string.Empty;

    public int ResourceType { get; set; }

    public string? CustomerActionText { get; set; }

    public int? Capacity { get; set; }

    public bool IsActive { get; set; }

    public bool AllowParallelUsage { get; set; }

    public bool CreatesOccupancy { get; set; } = true;

    public long? ResourceGroupId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public decimal? LayoutX { get; set; }

    public decimal? LayoutY { get; set; }

    public decimal? LayoutWidth { get; set; }

    public decimal? LayoutHeight { get; set; }

    public int LayoutRotationDeg { get; set; }

    public int LayoutShape { get; set; }

    public string? LayoutPointsJson { get; set; }
}