namespace BookingPlatform.Contracts.Appointments;

public sealed class AcceptRescheduleRequest
{
    public long AppointmentId { get; set; }
    public long ChangeRequestId { get; set; }
}