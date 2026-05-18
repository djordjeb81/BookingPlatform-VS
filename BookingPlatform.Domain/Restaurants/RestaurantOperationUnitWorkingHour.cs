using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOperationUnitWorkingHour : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public long OperationUnitId { get; set; }

    public RestaurantOperationUnit OperationUnit { get; set; } = null!;

    /// <summary>
    /// 1 = ponedeljak, 7 = nedelja
    /// </summary>
    public int DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsClosed { get; set; }
}