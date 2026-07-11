namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessTrainingPassUsageDto
{
    public long FitnessMemberTrainingPassId { get; set; }

    public string PlanName { get; set; } = string.Empty;

    public string ClassTypeName { get; set; } = string.Empty;

    public DateOnly ValidFromDate { get; set; }

    public DateOnly ValidToDate { get; set; }

    public int? TotalSessions { get; set; }

    public int UsedSessions { get; set; }

    public int? RemainingSessions { get; set; }

    public int? WeeklySessionLimit { get; set; }

    public int UsedThisWeek { get; set; }

    public bool CanUse { get; set; }

    public string Message { get; set; } = string.Empty;
}