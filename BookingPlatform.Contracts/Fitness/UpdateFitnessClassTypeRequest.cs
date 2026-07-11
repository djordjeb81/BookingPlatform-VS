namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessClassTypeRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DefaultDurationMin { get; set; }

    public int? DefaultCapacity { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }
}