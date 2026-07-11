namespace BookingPlatform.Domain.Fitness;

public enum FitnessSessionBookingStatus
{
    PendingApproval = 1,
    Booked = 2,
    Rejected = 3,
    CancelledByCustomer = 4,
    CancelledByBusiness = 5,
    Attended = 6,
    NoShow = 7
}