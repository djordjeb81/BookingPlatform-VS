namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessMemberSessionDebtDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long FitnessMemberId { get; set; }

    public string FitnessMemberName { get; set; } = string.Empty;

    public string FitnessMemberPhone { get; set; } = string.Empty;

    public long FitnessSessionId { get; set; }

    public long? FitnessClassTypeId { get; set; }

    public string FitnessClassTypeName { get; set; } = string.Empty;

    public long? FitnessMemberTrainingPassId { get; set; }

    public string TrainingPassText { get; set; } = string.Empty;

    public DateTime SessionStartAtUtc { get; set; }

    public string SessionDateText { get; set; } = string.Empty;

    public string SessionTimeText { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public int SessionsCount { get; set; }

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public DateTime? SettledAtUtc { get; set; }

    public DateTime? VoidedAtUtc { get; set; }

    public string? VoidReason { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}