namespace BookingPlatform.Contracts.Resources;

public sealed class ServiceResourceRequirementDto
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public bool IsRequired { get; set; }
}