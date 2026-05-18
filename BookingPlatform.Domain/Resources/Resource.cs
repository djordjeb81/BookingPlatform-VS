using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Resources;

public sealed class Resource : AuditableEntity
{
    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public ResourceType ResourceType { get; set; }

    public string? CustomerActionText { get; set; }

    public int? Capacity { get; set; }

    public bool AllowParallelUsage { get; set; }

    public bool IsActive { get; set; } = true;

    public bool CreatesOccupancy { get; set; } = true;

    public long? ResourceGroupId { get; set; }

    public ResourceGroup? ResourceGroup { get; set; }

    // Ugostiteljstvo / smeštaj:
    // sto, sala, soba ili apartman mogu pripadati određenoj sali/zoni.
    public long? RestaurantAreaId { get; set; }

    // Grafički prikaz na platnu sale.
    // Koordinate su relativne u odnosu na platno, npr. 1000x1000.
    public decimal? LayoutX { get; set; }

    public decimal? LayoutY { get; set; }

    public decimal? LayoutWidth { get; set; }

    public decimal? LayoutHeight { get; set; }

    public int LayoutRotationDeg { get; set; }

    public LayoutShapeType LayoutShape { get; set; } = LayoutShapeType.Rectangle;

    // Za nepravilan oblik.
    // Primer: [{"x":0,"y":0},{"x":200,"y":0},{"x":160,"y":120},{"x":0,"y":120}]
    public string? LayoutPointsJson { get; set; }
}