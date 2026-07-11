namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessSessionBookingStatusRequest
{
    public int Status { get; set; }

    public string? Note { get; set; }
}
