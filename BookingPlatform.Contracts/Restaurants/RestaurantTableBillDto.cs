namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantTableBillDto
{
    public long BusinessId { get; set; }

    public long TableSessionId { get; set; }

    public long RestaurantAreaId { get; set; }

    public long TableResourceId { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string? CustomerName { get; set; }

    public int? PartySize { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public int OrderCount { get; set; }

    public int ActiveOrderCount { get; set; }

    public bool HasActiveOrders { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal PaidAmount { get; set; }

    public decimal RemainingAmount { get; set; }

    public bool IsFullyPaid { get; set; }

    public string Currency { get; set; } = "RSD";

    public List<RestaurantTableBillLineDto> Lines { get; set; } = new();

    public List<RestaurantPaymentDto> Payments { get; set; } = new();
}