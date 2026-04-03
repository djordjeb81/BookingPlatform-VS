namespace BookingPlatform.Contracts.Services;

public sealed class CreateServiceStepRequest
{
    public long ServiceId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public bool ClientPresenceRequired { get; set; } = true;
    public bool SameStaffAsPrevious { get; set; }
}