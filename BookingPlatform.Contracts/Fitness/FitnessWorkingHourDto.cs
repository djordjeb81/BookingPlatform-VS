namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessWorkingHourDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long? FitnessRoomId { get; set; }

    public int DayOfWeek { get; set; }

    public string DayOfWeekText { get; set; } = string.Empty;

    public bool IsClosed { get; set; }

    public TimeOnly? OpenTime { get; set; }

    public TimeOnly? CloseTime { get; set; }

    public string TimeText { get; set; } = string.Empty;
}