namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessMembershipPlanDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public string FitnessClassTypeName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int? TotalSessions { get; set; }

    public string TotalSessionsText { get; set; } = string.Empty;

    public int? WeeklySessionLimit { get; set; }

    public string WeeklySessionLimitText { get; set; } = string.Empty;

    public int DefaultValidityDays { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; } = "RSD";

    public bool UnusedSessionsCarryOver { get; set; }

    public string CarryOverText { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string IsActiveText { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }

    public string DisplayText { get; set; } = string.Empty;
}