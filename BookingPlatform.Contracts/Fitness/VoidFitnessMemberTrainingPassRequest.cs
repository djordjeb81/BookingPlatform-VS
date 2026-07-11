namespace BookingPlatform.Contracts.Fitness;

public sealed class VoidFitnessMemberTrainingPassRequest
{
    public string Reason { get; set; } = string.Empty;

    public long? VoidedByUserId { get; set; }
}