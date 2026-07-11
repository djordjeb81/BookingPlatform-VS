namespace BookingPlatform.Contracts.Fitness;

public sealed class CustomerCreateFitnessBookingRequest
{
    public long FitnessSessionId { get; set; }

    public long? AppUserId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public string? CustomerName { get; set; }

    public string? CustomerPhone { get; set; }
}

public sealed class CustomerCancelFitnessBookingRequest
{
    public long? AppUserId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public string? CustomerPhone { get; set; }
}