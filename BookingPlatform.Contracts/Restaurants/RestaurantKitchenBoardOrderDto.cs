namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantKitchenBoardOrderDto
{
    public long OrderId { get; set; }

    public long BusinessId { get; set; }

    public long? RestaurantAreaId { get; set; }

    public long? TableResourceId { get; set; }

    public long? TableSessionId { get; set; }

    public int OrderType { get; set; }

    public string OrderTypeText { get; set; } = string.Empty;

    public int OrderSource { get; set; }

    public string OrderSourceText { get; set; } = string.Empty;

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public int KitchenDecisionStatus { get; set; }

    public string KitchenDecisionStatusText { get; set; } = string.Empty;

    public DateTime? KitchenAcceptedAtUtc { get; set; }

    public int? KitchenAcceptLaterMinutes { get; set; }

    public DateTime? KitchenRejectedAtUtc { get; set; }

    public string? KitchenRejectReason { get; set; }

    public string? KitchenRejectNote { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }

    public DateTime? RequestedPickupAtUtc { get; set; }

    public string? DeliveryAddress { get; set; }

    public string? DeliveryNote { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public decimal TotalAmount { get; set; }

    public string Currency { get; set; } = "RSD";

    public List<RestaurantOrderItemDto> Items { get; set; } = new();
}