namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessMemberTrainingPassDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long FitnessMemberId { get; set; }

    public string FitnessMemberName { get; set; } = string.Empty;

    public string FitnessMemberPhone { get; set; } = string.Empty;

    public long FitnessMembershipPlanId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public bool IsVoided { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public string VoidStatusText { get; set; } = string.Empty;

    public string PlanNameSnapshot { get; set; } = string.Empty;

    public string? FitnessClassTypeNameSnapshot { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public DateOnly ValidToDate { get; set; }

    public string ValidPeriodText { get; set; } = string.Empty;

    public int? TotalSessions { get; set; }

    public int UsedSessions { get; set; }

    public int? RemainingSessions { get; set; }

    public string SessionsText { get; set; } = string.Empty;

    public int? WeeklySessionLimit { get; set; }

    public int UsedThisWeek { get; set; }

    public string WeeklyLimitText { get; set; } = string.Empty;

    public decimal PricePaid { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime PaidAtUtc { get; set; }

    public bool IsActive { get; set; }

    public string IsActiveText { get; set; } = string.Empty;

    public bool IsCurrentlyValid { get; set; }

    public string CurrentStatusText { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string DisplayText { get; set; } = string.Empty;
}