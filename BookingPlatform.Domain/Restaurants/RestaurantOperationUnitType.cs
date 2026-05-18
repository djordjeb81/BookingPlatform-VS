namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantOperationUnitType
{
    DiningRoom = 1,       // Sala / restoran
    Kitchen = 2,          // Kuhinja
    TakeawayCounter = 3,  // Šalter / za poneti
    Delivery = 4,         // Dostava
    Reception = 5,        // Recepcija / portir
    Bar = 6,              // Šank
    Other = 99            // Ostalo
}