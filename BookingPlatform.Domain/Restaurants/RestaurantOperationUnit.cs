using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantOperationUnit : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public RestaurantOperationUnitType UnitType { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public bool ReceivesCustomerChat { get; set; }

    public ICollection<RestaurantOperationUnitWorkingHour> WorkingHours { get; set; } =
        new List<RestaurantOperationUnitWorkingHour>();
}