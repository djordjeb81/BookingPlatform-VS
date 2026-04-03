namespace BookingPlatform.Contracts.Services;

public sealed class ServiceDto
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public int EstimatedDurationMin { get; set; }
    public int BookingStrategyType { get; set; }
    public bool IsActive { get; set; }
}