namespace BookingPlatform.Contracts.Resources;

public sealed class UpdateServiceStepResourceRequirementRequest
{
    public long ServiceStepId { get; set; }
    public long ResourceId { get; set; }
    public bool IsRequired { get; set; }
    public int? SequenceOrder { get; set; }
    public int? UsageDurationMin { get; set; }
}