namespace BookingPlatform.Contracts.Services;

public sealed class ServiceStepDto
{
    public long Id { get; set; }
    public long ServiceId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public bool ClientPresenceRequired { get; set; }
    public bool SameStaffAsPrevious { get; set; }
}