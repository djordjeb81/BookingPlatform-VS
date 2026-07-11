namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessSessionTemplateDto
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

    public int DayOfWeek { get; set; }

    public string DayOfWeekText { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public int DurationMin { get; set; }

    public TimeOnly EndTime { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public DateOnly? ValidFromDate { get; set; }

    public DateOnly? ValidToDate { get; set; }

    public string? Note { get; set; }

    public string DisplayText { get; set; } = string.Empty;
}