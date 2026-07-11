namespace BookingPlatform.Contracts.Fitness;

public sealed class FitnessSettingsDto
{
    public long Id { get; set; }

    public long BusinessId { get; set; }

    public bool GroupClassesEnabled { get; set; }

    public bool IndividualTrainingEnabled { get; set; }

    public bool ReceivesCustomerMessages { get; set; }

    public bool MembershipsEnabled { get; set; }

    public int UnpaidMembershipBookingPolicy { get; set; }

    public string UnpaidMembershipBookingPolicyText { get; set; } = string.Empty;

    public int DefaultMembershipDurationDays { get; set; }

    public bool AllowCustomerCancelBooking { get; set; }

    public int CustomerCancelDeadlineMinutes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}