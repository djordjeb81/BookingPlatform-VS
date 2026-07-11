namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessSessionTemplateRequest
{
    public long FitnessRoomId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public int SessionType { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public int DurationMin { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public DateOnly? ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public string? Note { get; set; }
}