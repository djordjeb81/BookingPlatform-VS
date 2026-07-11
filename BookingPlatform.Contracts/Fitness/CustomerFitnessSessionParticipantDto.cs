namespace BookingPlatform.Contracts.Fitness;

public sealed class CustomerFitnessSessionParticipantDto
{
    public long BookingId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int BookingStatus { get; set; }

    public string BookingStatusText { get; set; } = string.Empty;
}