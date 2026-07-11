namespace BookingPlatform.Contracts.Fitness;

public sealed class UpdateFitnessSettingsRequest
{
    public bool GroupClassesEnabled { get; set; }

    public bool IndividualTrainingEnabled { get; set; }

    public bool ReceivesCustomerMessages { get; set; }

    public bool MembershipsEnabled { get; set; }

    public int UnpaidMembershipBookingPolicy { get; set; }

    public int DefaultMembershipDurationDays { get; set; }

    public bool AllowCustomerCancelBooking { get; set; }

    public int CustomerCancelDeadlineMinutes { get; set; }
}