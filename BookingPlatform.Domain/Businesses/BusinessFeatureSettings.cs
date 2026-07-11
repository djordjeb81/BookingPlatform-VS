namespace BookingPlatform.Domain.Businesses;

public sealed class BusinessFeatureSettings
{
    public long Id { get; set; }

    public long BusinessId { get; set; }
    public Business Business { get; set; } = null!;

    public bool ServiceAppointmentsEnabled { get; set; } = true;

    public bool TableReservationsEnabled { get; set; }
    public bool HasCustomerSeating { get; set; }
    public bool FoodOrdersEnabled { get; set; }
    public bool DrinkOrdersEnabled { get; set; }
    public bool TakeawayOrdersEnabled { get; set; }
    public bool DeliveryOrdersEnabled { get; set; }

    public bool EventHallReservationsEnabled { get; set; }

    public bool AccommodationEnabled { get; set; }

    public bool ReviewsEnabled { get; set; } = true;
}