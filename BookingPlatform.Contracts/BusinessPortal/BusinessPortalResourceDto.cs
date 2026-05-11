namespace BookingPlatform.Contracts.BusinessPortal;

public sealed class BusinessPortalResourceDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int ResourceType { get; set; }

    public int? Capacity { get; set; }

    public bool AllowParallelUsage { get; set; }

    public bool CreatesOccupancy { get; set; }

    public bool IsActive { get; set; }
}