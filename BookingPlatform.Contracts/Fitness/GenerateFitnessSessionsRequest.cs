namespace BookingPlatform.Contracts.Fitness;

public sealed class GenerateFitnessSessionsRequest
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public bool OverwriteExistingGeneratedSessions { get; set; }
}