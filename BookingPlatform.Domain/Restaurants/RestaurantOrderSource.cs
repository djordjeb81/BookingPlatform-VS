namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantOrderSource
{
    RestaurantDesk = 1,   // sala / konobar / restoran
    KitchenDesk = 2,     // kuhinja / brza hrana
    AndroidCustomer = 3,  // klijent preko Android aplikacije
    WebCustomer = 4,      // kasnije web
    Admin = 5,             // ručni/admin unos
    Other = 99          // ostalo
}