namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessSessionBookingDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public long FitnessSessionId { get; set; }

    public long? FitnessMemberTrainingPassId { get; set; }

    public bool ConsumesTrainingPassSession { get; set; }

    public long? FitnessMemberId { get; set; }

    public long? CustomerProfileId { get; set; }

    public long? BusinessCustomerId { get; set; }

    public long? AppUserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public int Status { get; set; }

    public string StatusText { get; set; } = string.Empty;

    public bool MembershipWasActiveAtBooking { get; set; }

    public string? MembershipWarningText { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public DateTime? AttendedAtUtc { get; set; }

    public DateTime? NoShowAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}