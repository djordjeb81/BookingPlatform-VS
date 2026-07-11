namespace BookingPlatform.Domain.Fitness;

public enum FitnessUnpaidMembershipBookingPolicy
{
    Block = 1,
    Allow = 2,
    AllowWithNotification = 3,
    RequireApproval = 4
}