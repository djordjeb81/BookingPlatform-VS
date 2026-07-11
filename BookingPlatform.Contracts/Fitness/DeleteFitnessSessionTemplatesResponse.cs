namespace BookingPlatform.Contracts.Fitness;

public sealed class DeleteFitnessSessionTemplatesResponse
{
    public int DeletedCount { get; set; }

    public int SkippedWithGeneratedSessionsCount { get; set; }

    public string Message { get; set; } = string.Empty;
}