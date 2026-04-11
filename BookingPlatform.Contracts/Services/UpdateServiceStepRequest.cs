namespace BookingPlatform.Contracts.Services;

public sealed class UpdateServiceStepRequest
{
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DurationMin { get; set; }
    public bool ClientPresenceRequired { get; set; }
    public bool SameStaffAsPrevious { get; set; }
}