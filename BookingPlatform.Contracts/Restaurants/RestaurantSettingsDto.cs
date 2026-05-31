namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantSettingsDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public int PreparationReminderBufferMin { get; set; }

    public int ScheduledOrderMinLeadTimeMin { get; set; }

    public int ScheduledOrderMaxDaysAhead { get; set; }

    public bool IsScheduledOrderingEnabled { get; set; }

    public bool IsDeliveryEnabled { get; set; }

    public bool IsDeliveryLocationRequired { get; set; }
}