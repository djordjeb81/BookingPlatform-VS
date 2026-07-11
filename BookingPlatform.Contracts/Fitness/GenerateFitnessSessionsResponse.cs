namespace BookingPlatform.Contracts.Fitness;

public sealed class GenerateFitnessSessionsResponse
{
    public int CreatedCount { get; set; }

    public int SkippedExistingCount { get; set; }

    public int SkippedInvalidCount { get; set; }

    public string Message { get; set; } = string.Empty;
}