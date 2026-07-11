namespace BookingPlatform.Contracts.Fitness;

public sealed class DeleteGeneratedFitnessSessionsResponse
{
    public int DeletedSessionsCount { get; set; }

    public int DeletedBookingsCount { get; set; }

    public int SkippedWithBookingsCount { get; set; }

    public string Message { get; set; } = string.Empty;
}