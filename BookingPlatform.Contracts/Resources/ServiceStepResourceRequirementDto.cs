namespace BookingPlatform.Contracts.Resources;

public sealed class ServiceStepResourceRequirementDto
{
    public long Id { get; set; }
    public long ServiceStepId { get; set; }
    public long ResourceId { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public int ResourceType { get; set; }
    public bool IsRequired { get; set; }
    public int? SequenceOrder { get; set; }
    public int? UsageDurationMin { get; set; }
}