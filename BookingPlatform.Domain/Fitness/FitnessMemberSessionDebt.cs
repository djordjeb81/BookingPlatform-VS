using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessMemberSessionDebt : Entity
{
    public long BusinessId { get; set; }

    public long FitnessMemberId { get; set; }

    public long FitnessSessionId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public long? FitnessMemberTrainingPassId { get; set; }

    public int SessionsCount { get; set; } = 1;

    public FitnessMemberSessionDebtStatus Status { get; set; } = FitnessMemberSessionDebtStatus.Open;

    public DateTime? SettledAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Business Business { get; set; } = null!;

    public FitnessMember FitnessMember { get; set; } = null!;

    public FitnessSession FitnessSession { get; set; } = null!;

    public FitnessClassType? FitnessClassType { get; set; }

    public FitnessMemberTrainingPass? FitnessMemberTrainingPass { get; set; }
}