namespace BookingPlatform.Contracts.Restaurants;

public enum RestaurantAreaVisualStatusDto
{
    Available = 0,
    Occupied = 1,
    PendingReservation = 2,
    Inactive = 3
}

public enum RestaurantResourceVisualStatusDto
{
    Available = 0,
    Occupied = 1,
    ReservedLater = 2,
    PendingReservation = 3,
    Inactive = 4,
    AreaOccupied = 5
}