namespace BookingPlatform.Domain.Restaurants;

public enum RestaurantOrderMessageType
{
    Text = 0,

    OrderCreated = 1,
    OrderSubmitted = 2,

    KitchenAccepted = 10,
    KitchenWaitingProposed = 11,
    KitchenRejected = 12,

    CustomerAcceptedWaiting = 20,
    CustomerRejectedWaiting = 21,

    OrderPreparing = 30,
    OrderReady = 31,
    OrderServed = 32,
    OrderCancelled = 33,

    InternalManualMessage = 40
}