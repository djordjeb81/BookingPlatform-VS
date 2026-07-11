using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Restaurants;

public sealed class SharedRestaurantOrderItem : AuditableEntity
{
    public long SharedRestaurantOrderId { get; set; }

    public SharedRestaurantOrder SharedRestaurantOrder { get; set; } = null!;

    public long BusinessId { get; set; }

    public string BusinessNameSnapshot { get; set; } = string.Empty;

    public long AddedByCustomerProfileId { get; set; }

    public long? AddedByAppUserId { get; set; }

    public string AddedByDisplayNameSnapshot { get; set; } = string.Empty;

    public string? OrderPersonName { get; set; }

    public long MenuItemId { get; set; }

    public string MenuItemNameSnapshot { get; set; } = string.Empty;

    public decimal UnitPriceSnapshot { get; set; }

    public int Quantity { get; set; }

    public decimal LineSubtotal { get; set; }

    public bool SendToKitchenSnapshot { get; set; } = true;

    public string? Note { get; set; }

    public long? SourceSharedRestaurantOrderId { get; set; }

    public long? SourceSharedRestaurantOrderItemId { get; set; }

    public long? SourceChatMessageId { get; set; }

    public ICollection<SharedRestaurantOrderItemOption> Options { get; set; } =
        new List<SharedRestaurantOrderItemOption>();
}
