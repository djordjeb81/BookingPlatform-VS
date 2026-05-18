namespace BookingPlatform.Contracts.Restaurants;

public sealed class CreateRestaurantPaymentRequest
{
    public long TableSessionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "RSD";

    public int Method { get; set; } = 1;

    public string? Note { get; set; }
}