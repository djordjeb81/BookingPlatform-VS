namespace BookingPlatform.Contracts.Fitness;

public sealed class DeleteFitnessSessionTemplatesRequest
{
    public List<long> TemplateIds { get; set; } = new();
}