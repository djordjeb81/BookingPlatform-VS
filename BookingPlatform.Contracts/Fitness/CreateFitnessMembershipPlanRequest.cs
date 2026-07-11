namespace BookingPlatform.Contracts.Fitness;

public sealed class CreateFitnessMembershipPlanRequest
{
    public long? FitnessClassTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? TotalSessions { get; set; }

    public int? WeeklySessionLimit { get; set; }

    public int DefaultValidityDays { get; set; } = 30;

    public decimal Price { get; set; }

    public string Currency { get; set; } = "RSD";

    public bool UnusedSessionsCarryOver { get; set; } = false;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    public string? Note { get; set; }
}