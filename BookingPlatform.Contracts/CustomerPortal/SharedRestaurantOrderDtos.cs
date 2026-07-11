using BookingPlatform.Contracts.Restaurants;

namespace BookingPlatform.Contracts.CustomerPortal;

public sealed class SharedRestaurantOrderDto
{
    public long Id { get; set; }

    public long OwnerCustomerProfileId { get; set; }

    public long? OwnerAppUserId { get; set; }

    public string OwnerDisplayName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Note { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public decimal SubtotalAmount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<SharedRestaurantOrderItemDto> Items { get; set; } = new();
}


public sealed class UpdateSharedRestaurantOrderItemRequest
{
    public int Quantity { get; set; }

    public string? OrderPersonName { get; set; }

    public string? Note { get; set; }

    public List<RestaurantOrderItemAddonSelectionDto>? Addons { get; set; }
}

public sealed class SharedRestaurantOrderItemDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public long AddedByCustomerProfileId { get; set; }

    public long? AddedByAppUserId { get; set; }

    public string AddedByDisplayName { get; set; } = string.Empty;

    public string? OrderPersonName { get; set; }

    public long MenuItemId { get; set; }

    public string MenuItemName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public decimal LineSubtotal { get; set; }

    public string? Note { get; set; }

    public List<SharedRestaurantOrderItemAddonDto> Addons { get; set; } = new();
}

public sealed class SharedRestaurantOrderItemAddonDto
{
    public long? RestaurantAddonId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal PriceDelta { get; set; }

    public int AmountMode { get; set; }

    public string AmountModeText { get; set; } = string.Empty;
}

public sealed class CreateSharedRestaurantOrderRequest
{
    public string? Title { get; set; }

    public string? Note { get; set; }

    public List<CreateSharedRestaurantOrderItemRequest> Items { get; set; } = new();
}

public sealed class CreateSharedRestaurantOrderItemRequest
{
    public long BusinessId { get; set; }

    public long MenuItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public string? OrderPersonName { get; set; }

    public string? Note { get; set; }

    public List<RestaurantOrderItemAddonSelectionDto> Addons { get; set; } = new();
}

public sealed class SendSharedRestaurantOrderToChatRequest
{
    public long ConversationId { get; set; }
}

public sealed class AcceptSharedRestaurantOrderChatRequest
{
    public long? TargetSharedRestaurantOrderId { get; set; }

    public bool CreateNew { get; set; }
}

public sealed class SubmitSharedRestaurantOrderRequest
{
    public List<SubmitSharedRestaurantOrderBusinessRequest> Businesses { get; set; } = new();
}

public sealed class SubmitSharedRestaurantOrderBusinessRequest
{
    public long BusinessId { get; set; }

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
}

public sealed class SubmitSharedRestaurantOrderResponse
{
    public long SharedRestaurantOrderId { get; set; }

    public List<RestaurantOrderDto> Orders { get; set; } = new();
}
