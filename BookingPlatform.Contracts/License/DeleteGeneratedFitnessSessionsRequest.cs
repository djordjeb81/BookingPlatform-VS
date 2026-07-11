namespace BookingPlatform.Contracts.Fitness;

public sealed class DeleteGeneratedFitnessSessionsRequest
{
    public DateOnly FromDate { get; set; }

    public DateOnly ToDate { get; set; }

    public bool DeleteSessionsWithBookings { get; set; }
}