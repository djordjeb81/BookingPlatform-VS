namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessSessionTemplateRequest
{
    public long FitnessRoomId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public long? TrainerStaffMemberId { get; set; }

    public int SessionType { get; set; }

    public int DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public int DurationMin { get; set; } = 60;

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateOnly? ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public string? Note { get; set; }
}