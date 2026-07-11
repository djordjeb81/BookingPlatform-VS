using BookingPlatform.Domain.Businesses;
using BookingPlatform.Domain.Common;

namespace BookingPlatform.Domain.Fitness;

public sealed class FitnessSettings : AuditableEntity
{
    public long BusinessId { get; set; }

    public Business Business { get; set; } = null!;

    public bool GroupClassesEnabled { get; set; } = true;

    public bool IndividualTrainingEnabled { get; set; }

    public bool ReceivesCustomerMessages { get; set; } = true;

    public bool MembershipsEnabled { get; set; } = true;

    public FitnessUnpaidMembershipBookingPolicy UnpaidMembershipBookingPolicy { get; set; } =
        FitnessUnpaidMembershipBookingPolicy.AllowWithNotification;

    public int DefaultMembershipDurationDays { get; set; } = 30;

    public bool AllowCustomerCancelBooking { get; set; } = true;

    public int CustomerCancelDeadlineMinutes { get; set; } = 120;
}