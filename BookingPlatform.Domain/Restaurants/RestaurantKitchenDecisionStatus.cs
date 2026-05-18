namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantKitchenDecisionStatus
{
    None = 0,
    Accepted = 1,

    // Za sada ostavljamo staru vrednost zbog postojećeg koda i baze.
    // Značenje: kuhinja je predložila čekanje, čeka se odgovor restorana/klijenta.
    AcceptedLater = 2,

    Rejected = 3,

    WaitingAcceptedByCustomer = 4,
    WaitingRejectedByCustomer = 5
}