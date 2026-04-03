namespace BookingPlatform.Domain.Appointments;

public enum AppointmentStatus
{
    PendingApproval = 1,
    Confirmed = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    NoShow = 6
}