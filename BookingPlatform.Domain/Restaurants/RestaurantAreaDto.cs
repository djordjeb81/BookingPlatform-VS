namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantAreaDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public string? BoundaryPointsJson { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsReservableAsWhole { get; set; }

    public long? WholeAreaResourceId { get; set; }
}