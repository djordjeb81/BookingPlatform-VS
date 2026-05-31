namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantDeliveryZoneDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal DeliveryFeeAmount { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}

public sealed class CreateRestaurantDeliveryZoneRequest
{
    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal DeliveryFeeAmount { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}

public sealed class UpdateRestaurantDeliveryZoneRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal DeliveryFeeAmount { get; set; }

    public decimal MinimumOrderAmount { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}