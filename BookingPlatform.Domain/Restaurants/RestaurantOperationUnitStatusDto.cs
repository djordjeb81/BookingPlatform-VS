namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOperationUnitStatusDto
{
    public long OperationUnitId { get; set; }

    public long BusinessId { get; set; }

    public int UnitType { get; set; }

    public string UnitTypeText { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool HasWorkingHoursForToday { get; set; }

    public bool IsClosedToday { get; set; }

    public bool IsWorkingNow { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string? TodayStartTime { get; set; }

    public string? TodayEndTime { get; set; }
}