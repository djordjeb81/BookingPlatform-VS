namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantOrderItemRequest
{
    public long? OrderGuestId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Note { get; set; }

    // Staro polje ostavljamo za kompatibilnost, ali novu logiku radimo preko Addons.
    public List<long> MenuItemOptionIds { get; set; } = new();

    public List<RestaurantOrderItemAddonSelectionDto> Addons { get; set; } = new();
}