namespace BookingPlatform.Contracts.Appointments;

public sealed class RejectAppointmentRequest
{
    public long AppointmentId { get; set; }
    public string? Reason { get; set; }
}