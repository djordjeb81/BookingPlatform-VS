namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessClassTypeDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DefaultDurationMin { get; set; }

    public int? DefaultCapacity { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}