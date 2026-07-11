namespace BookingPlatform.Domain.Restaurants;

public enum SharedRestaurantOrderStatus
{
    Draft = 0,
    SentToChat = 1,
    Submitted = 2,
    Cancelled = 3,
    Completed = 4
}