namespace BookingPlatform.Contracts.Fitness;

public sealed class CustomerFitnessSessionDetailsDto
{
    public CustomerFitnessSessionDto Session { get; set; } = new();

    public List<CustomerFitnessSessionParticipantDto> Participants { get; set; } = new();
}