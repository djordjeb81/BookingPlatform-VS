namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessSessionDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long FitnessRoomId { get; set; }

    public string FitnessRoomName { get; set; } = string.Empty;

    public long? FitnessClassTypeId { get; set; }

    public string? FitnessClassTypeName { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public string? TrainerName { get; set; }

    public int SessionType { get; set; }

    public string SessionTypeText { get; set; } = string.Empty;

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public int Capacity { get; set; }

    public int BookedCount { get; set; }

    public string CapacityText { get; set; } = string.Empty;

    public bool IsFull { get; set; }

    public bool CanBook { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}