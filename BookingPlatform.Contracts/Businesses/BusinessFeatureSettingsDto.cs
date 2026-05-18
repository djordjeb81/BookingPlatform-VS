namespace BookingPlatform.Contracts.Businesses;

public sealed class BusinessFeatureSettingsDto
{
    public bool ServiceAppointmentsEnabled { get; set; } = true;

    public bool TableReservationsEnabled { get; set; }

    public bool FoodOrdersEnabled { get; set; }

    public bool DrinkOrdersEnabled { get; set; }

    public bool TakeawayOrdersEnabled { get; set; }

    public bool DeliveryOrdersEnabled { get; set; }

    public bool EventHallReservationsEnabled { get; set; }

    public bool AccommodationEnabled { get; set; }

    public bool ReviewsEnabled { get; set; } = true;
}