namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantAreaReservationStatus
{
    PendingApproval = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Arrived = 5,
    NoShow = 6,
    Completed = 7
}