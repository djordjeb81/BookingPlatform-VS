namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessSessionRequest
{
    public long FitnessRoomId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public int SessionType { get; set; }

    public DateTime StartAtUtc { get; set; }

    public DateTime EndAtUtc { get; set; }

    public int Capacity { get; set; }

    public int Status { get; set; }

    public string? Note { get; set; }
}