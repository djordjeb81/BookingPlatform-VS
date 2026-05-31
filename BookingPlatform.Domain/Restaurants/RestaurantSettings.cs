using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class RestaurantSettings : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public int PreparationReminderBufferMin { get; set; } = 10;

    public int ScheduledOrderMinLeadTimeMin { get; set; } = 30;

    public int ScheduledOrderMaxDaysAhead { get; set; } = 7;

    public bool IsScheduledOrderingEnabled { get; set; } = true;

    public bool IsDeliveryEnabled { get; set; } = true;

    public bool IsDeliveryLocationRequired { get; set; } = false;
}