namespace BookingPlatform.Contracts.Services;

public sealed class UpdateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public int EstimatedDurationMin { get; set; }
    public int BookingStrategyType { get; set; }
    public bool IsActive { get; set; }
}