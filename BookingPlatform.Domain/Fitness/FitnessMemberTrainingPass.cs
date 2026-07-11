using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessMemberTrainingPass : Entity
{
    public long BusinessId { get; set; }

    public long FitnessMemberId { get; set; }

    public long FitnessMembershipPlanId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public string PlanNameSnapshot { get; set; } = string.Empty;

    public string? FitnessClassTypeNameSnapshot { get; set; }

    public DateOnly ValidFromDate { get; set; }

    public List<FitnessMemberSessionDebt> SessionDebts { get; set; } = new();

    public bool IsVoided { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public long? VoidedByUserId { get; set; }

    public DateOnly ValidToDate { get; set; }

    public int? TotalSessions { get; set; }

    public int? WeeklySessionLimit { get; set; }

    public decimal PricePaid { get; set; }

    public string Currency { get; set; } = "RSD";

    public DateTime PaidAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Note { get; set; }

    public Business Business { get; set; } = null!;

    public FitnessMember FitnessMember { get; set; } = null!;

    public FitnessMembershipPlan FitnessMembershipPlan { get; set; } = null!;

    public FitnessClassType? FitnessClassType { get; set; }

    public List<FitnessSessionBooking> Bookings { get; set; } = new();
}