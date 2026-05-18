namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantTableReservationStatus
{
    PendingApproval = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Arrived = 5,
    NoShow = 6
}