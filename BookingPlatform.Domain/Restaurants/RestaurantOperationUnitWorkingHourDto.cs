namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOperationUnitWorkingHourDto
{
    public long Id { get; set; }

    public long OperationUnitId { get; set; }

    public int DayOfWeek { get; set; }

    public string StartTime { get; set; } = "09:00";

    public string EndTime { get; set; } = "17:00";

    public bool IsClosed { get; set; }
}