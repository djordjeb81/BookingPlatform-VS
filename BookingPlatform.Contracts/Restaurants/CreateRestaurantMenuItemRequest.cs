namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantMenuItemRequest
{
    public long BusinessId { get; set; }

    public long CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "RSD";

    public bool IsAvailable { get; set; } = true;

    public bool SendToKitchen { get; set; } = true;

    public int PreparationTimeMin { get; set; }

    public int DisplayOrder { get; set; }
}