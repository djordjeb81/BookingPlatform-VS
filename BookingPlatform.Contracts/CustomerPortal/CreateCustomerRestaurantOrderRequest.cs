using BookingPlatform.Contracts.Restaurants;

namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class CreateCustomerRestaurantOrderRequest
{
    public int OrderType { get; set; }

    public DateTime? RequestedPickupAtUtc { get; set; }

    public bool IsScheduledOrder { get; set; }

    public string? DeliveryAddress { get; set; }

    public long? DeliveryZoneId { get; set; }

    public string? DeliveryNote { get; set; }

    public double? DeliveryLatitude { get; set; }

    public double? DeliveryLongitude { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }

    public List<CreateCustomerRestaurantOrderItemRequest> Items { get; set; } = new();
}

public sealed class CreateCustomerRestaurantOrderItemRequest
{
    public long MenuItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Note { get; set; }

    public List<RestaurantOrderItemAddonSelectionDto> Addons { get; set; } = new();
}

public sealed class UpdateCustomerRestaurantOrderItemsRequest
{
    public List<CreateCustomerRestaurantOrderItemRequest> Items { get; set; } = new();
}
