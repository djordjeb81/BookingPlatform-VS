namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessSessionBookingRequest
{
    public long FitnessSessionId { get; set; }

    public long? FitnessMemberId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? AppUserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;
}
