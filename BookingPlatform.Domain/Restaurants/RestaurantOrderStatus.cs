namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantOrderStatus
{
    Draft = 1,
    Submitted = 2,
    Preparing = 3,
    Ready = 4,
    Served = 5,
    Cancelled = 6
}