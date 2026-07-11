namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessRoomDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public bool AllowsGroupClasses { get; set; }

    public bool AllowsIndividualTraining { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}