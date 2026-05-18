namespace BookingPlatform.Contracts.Restaurants;

public sealed class UpdateRestaurantOrderGuestRequest
{
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }
}