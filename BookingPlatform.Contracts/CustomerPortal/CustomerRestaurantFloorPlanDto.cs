namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CustomerRestaurantFloorPlanDto
{
    public long BusinessId { get; set; }

    public long RestaurantAreaId { get; set; }

    public string AreaName { get; set; } = string.Empty;

    public decimal CanvasWidth { get; set; }

    public decimal CanvasHeight { get; set; }

    public string? BoundaryPointsJson { get; set; }

    public DateTime StatusAtUtc { get; set; }

    public List<CustomerRestaurantLayoutElementDto> Elements { get; set; } = new();

    public List<CustomerRestaurantFloorPlanTableDto> Tables { get; set; } = new();
}

public sealed class CustomerRestaurantLayoutElementDto
{
    public long Id { get; set; }

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
}

public sealed class CustomerRestaurantFloorPlanTableDto
{
    public long ResourceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTime? BusyUntilUtc { get; set; }

    public DateTime? ReservedFromUtc { get; set; }

    public DateTime? ReservedUntilUtc { get; set; }

    public decimal? LayoutX { get; set; }

    public decimal? LayoutY { get; set; }

    public decimal? LayoutWidth { get; set; }

    public decimal? LayoutHeight { get; set; }

    public int LayoutRotationDeg { get; set; }

    public int LayoutShape { get; set; }

    public string? LayoutPointsJson { get; set; }
}

public sealed class CustomerRestaurantAreaDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }
}

public sealed class CustomerRestaurantTableScheduleItemDto
{
    public DateTime FromUtc { get; set; }

    public DateTime UntilUtc { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;
}