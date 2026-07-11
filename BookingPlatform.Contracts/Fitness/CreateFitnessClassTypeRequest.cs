namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessClassTypeRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DefaultDurationMin { get; set; } = 60;

    public int? DefaultCapacity { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}