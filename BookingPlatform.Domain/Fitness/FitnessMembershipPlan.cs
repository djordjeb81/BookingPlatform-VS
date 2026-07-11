using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessMembershipPlan : Entity
{
    public long BusinessId { get; set; }

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

    public Business Business { get; set; } = null!;

    public FitnessClassType? FitnessClassType { get; set; }

    public List<FitnessMemberTrainingPass> MemberTrainingPasses { get; set; } = new();
}