namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantAreaRequest
{
    public string Name { get; set; } = string.Empty;

    public int? Capacity { get; set; }

    public int CanvasWidth { get; set; } = 1000;

    public int CanvasHeight { get; set; } = 1000;

    public string? BoundaryPointsJson { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; }

    public bool IsReservableAsWhole { get; set; }

    public long? WholeAreaResourceId { get; set; }
}