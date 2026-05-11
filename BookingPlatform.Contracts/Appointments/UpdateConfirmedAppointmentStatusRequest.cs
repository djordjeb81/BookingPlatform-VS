namespace BookingPlatform.Contracts.Appointments;

public sealed class UpdateConfirmedAppointmentStatusRequest
{
    public long AppointmentId { get; set; }
    public string? Note { get; set; }
}