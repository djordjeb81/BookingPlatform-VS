using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Services;

public sealed class Service : AuditableEntity
{
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? BasePrice { get; set; }
    public int EstimatedDurationMin { get; set; }
    public BookingStrategyType BookingStrategyType { get; set; }
    public bool IsActive { get; set; } = true;
}