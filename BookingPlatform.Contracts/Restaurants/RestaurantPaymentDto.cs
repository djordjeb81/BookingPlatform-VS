namespace BookingPlatform.Contracts.Restaurants;

public sealed class RestaurantPaymentDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long TableSessionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public int Method { get; set; }

    public string MethodText { get; set; } = string.Empty;

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime PaidAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}