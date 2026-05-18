namespace BookingPlatform.Contracts.Restaurants;

public sealed class ReplaceRestaurantOperationUnitWorkingHoursRequestDto
{
    public List<RestaurantOperationUnitWorkingHourInputDto> WorkingHours { get; set; } = new();
}

public sealed class RestaurantOperationUnitWorkingHourInputDto
{
    public int DayOfWeek { get; set; }

    public string StartTime { get; set; } = "09:00";

    public string EndTime { get; set; } = "17:00";

    public bool IsClosed { get; set; }
}