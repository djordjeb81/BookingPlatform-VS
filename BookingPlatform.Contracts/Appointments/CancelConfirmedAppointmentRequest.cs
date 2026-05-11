namespace BookingPlatform.Contracts.Appointments;

public sealed class CancelConfirmedAppointmentRequest
{
    public long AppointmentId { get; set; }
    public string? Reason { get; set; }
}