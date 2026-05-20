namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderItemAddonSelectionDto
{
    public long AddonId { get; set; }

    // 0 = normalno, 1 = malo, 2 = više
    public int AmountMode { get; set; }
}