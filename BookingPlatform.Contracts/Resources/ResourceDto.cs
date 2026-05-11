namespace BookingPlatform.Contracts.Resources;

public sealed class ResourceDto
{
    public long Id { get; set; }
    public long BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public string? CustomerActionText { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; }
    public bool AllowParallelUsage { get; set; }
    public bool CreatesOccupancy { get; set; } = true;

    public long? ResourceGroupId { get; set; }
    public string? ResourceGroupName { get; set; }
}