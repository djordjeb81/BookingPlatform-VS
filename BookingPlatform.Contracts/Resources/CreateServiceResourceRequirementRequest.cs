namespace BookingPlatform.Contracts.Resources;

public sealed class CreateServiceResourceRequirementRequest
{
    public long ServiceId { get; set; }
    public long ResourceId { get; set; }
    public bool IsRequired { get; set; } = true;
}