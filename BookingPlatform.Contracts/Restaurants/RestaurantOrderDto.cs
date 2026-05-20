namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantOrderDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public DateOnly OrderDateLocal { get; set; }

    public int DailyOrderNumber { get; set; }

    public string DisplayOrderNumberText { get; set; } = string.Empty;

    public long? RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public long? TableSessionId { get; set; }

    public int OrderType { get; set; }

    public int OrderSource { get; set; }

    public string OrderSourceText { get; set; } = string.Empty;

    public string OrderTypeText { get; set; } = string.Empty;

    public DateTime? RequestedPickupAtUtc { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? DeliveryNote { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public string? Note { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public int KitchenDecisionStatus { get; set; }

    public string KitchenDecisionStatusText { get; set; } = string.Empty;

    public DateTime? KitchenAcceptedAtUtc { get; set; }

    public int? KitchenAcceptLaterMinutes { get; set; }

    public DateTime? KitchenRejectedAtUtc { get; set; }

    public string? KitchenRejectReason { get; set; }

    public string? KitchenRejectNote { get; set; }

    public decimal SubtotalAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime? SubmittedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public List<RestaurantOrderGuestDto> Guests { get; set; } = new();

    public List<RestaurantOrderItemDto> Items { get; set; } = new();
}